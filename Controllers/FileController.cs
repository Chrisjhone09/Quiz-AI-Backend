using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using Microsoft.Net.Http.Headers;
using System.Net.Http.Headers;
using back.Dtos;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.SignalR;
using back.Hubs;

namespace back.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly IHubContext<QuizHub> _hub;

        public FileController(IHubContext<QuizHub> hub)
        {
            _hub = hub;
        }

        [HttpPost("upload-file")]
        public async Task<IActionResult> UploadFile([FromForm] FormDataDto formData)
        {
            var name = HeaderNames.AcceptEncoding;

            //try { }
            //catch (Exception ex) { throw new Exception("An error occurred while processing the file.", ex); }

            //var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

            //if (!Directory.Exists(uploadsFolder))
            //    Directory.CreateDirectory(uploadsFolder);

            //var fileName = Guid.NewGuid().ToString() + Path.GetExtension(formData.File.FileName);

            //var filePath = Path.Combine(uploadsFolder, fileName);

            //using (var stream = new FileStream(filePath, FileMode.Create))
            //{
            //    await formData.File.CopyToAsync(stream);
            //}

            string apiKey = "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";

            string text = string.Empty;

            var contentType = formData.File.ContentType ?? string.Empty;

            if (contentType == "application/pdf")
            {
                var sb = new StringBuilder();
                try
                {
                    using var reader = new PdfReader(formData.File.OpenReadStream());
                    using var pdfDoc = new iText.Kernel.Pdf.PdfDocument(reader);
                    for (int pageNum = 1; pageNum <= pdfDoc.GetNumberOfPages(); pageNum++)
                    {
                        var page = pdfDoc.GetPage(pageNum);
                        var pageText = PdfTextExtractor.GetTextFromPage(page);
                        sb.AppendLine(pageText);
                    }
                }
                catch
                {
                }

                text = sb.ToString();
            }
            else if (contentType == "application/vnd.openxmlformats-officedocument.presentationml.presentation")
            {
                var sb = new StringBuilder();
                try
                {
                    using var presentation = PresentationDocument.Open(formData.File.OpenReadStream(), false);
                    var presentationPart = presentation.PresentationPart;
                    if (presentationPart != null)
                    {
                        foreach (var slidePart in presentationPart.SlideParts)
                        {
                            var texts = slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>();
                            foreach (var t in texts)
                            {
                                sb.Append(t.Text);
                                sb.Append(' ');
                            }
                        }
                    }
                }
                catch
                {
                    // ignore
                }

                text = sb.ToString();
            }
            else if (contentType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
            {
                try
                {
                    using var doc = WordprocessingDocument.Open(formData.File.OpenReadStream(), false);
                    text = doc.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
                }
                catch
                {
                    text = string.Empty;
                }
            }

            text = Regex.Replace(text ?? string.Empty, @"-+", "");     
            text = Regex.Replace(text, @"\r\n|\r|\n", " "); 
            text = Regex.Replace(text, @"\s+", " ");       
            text = text.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                return BadRequest("Could not extract text from the provided file.");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "OpenAI API key is not configured.");
            }

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            string selectedTypes = string.Join(", ",
    new[]
    {
        formData.QuestionTypes.MultipleChoice == true ? "Multiple Choice" : null,
        formData.QuestionTypes.TrueFalse == true ? "True / False" : null,
        formData.QuestionTypes.Identification == true ? "Identification" : null
    }.Where(x => x != null));

            string prompt = $@"
                                Generate a quiz in VALID JSON format based strictly on the topic below.

                                Topic:
                                {text}

                                Difficulty:
                                {formData.Difficulty}

                                Allowed Question Types:
                                {selectedTypes}

                                Number of Questions:
                                {formData.NumQuestions}

                                Output rules:
                                - Return ONLY valid JSON
                                - No markdown
                                - No explanations
                                - No extra text
                                - Do NOT wrap in code blocks
                                - Ensure JSON is parsable

                                Required JSON structure:
                                {{
                                  ""fileName"": ""string"",
                                  ""questions"": [
                                    {{
                                      ""text"": ""string"",
                                      ""type"": ""Multiple Choice | True / False | Identification"",
                                      ""options"": [""string""],
                                      ""correct"": number,
                                      ""answer"": ""string (only for Identification, otherwise null or omit)""
                                    }}
                                  ]
                                }}

                                Strict rules:
                                - Generate EXACTLY {formData.NumQuestions} questions
                                - Use ONLY these allowed question types: {selectedTypes}
                                - Match difficulty level: {formData.Difficulty}
                                - Distribute questions naturally across selected types
                                - Avoid duplicates

                                Multiple Choice rules:
                                - Exactly 4 options
                                - ""correct"" must be 0-based index
                                - Options must be distinct and relevant

                                True / False rules:
                                - Options must be [""True"", ""False""]
                                - ""correct"" must be 0 or 1

                                Identification rules:
                                - ""options"" must be []
                                - ""correct"" must be -1
                                - Must include ""answer""

                                Content rules:
                                - Questions must be based ONLY on the topic
                                - Ensure academic correctness
                                - Ensure clarity
                                - Mix conceptual + factual questions

                                File name rule:
                                - ""fileName"" must be a clean quiz title related to topic

                                Final validation before output:
                                - JSON valid
                                - Correct indexes valid
                                - Question count exact
                                ";
            var payload = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = "You are a strict assistant that outputs ONLY the requested JSON with no additional text or formatting." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.7
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            var json = JsonSerializer.Serialize(payload, jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await http.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Return the raw response for debugging
                return StatusCode((int)response.StatusCode, responseString);
            }

            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;
                var choices = root.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var message = choices[0].GetProperty("message");
                    var assistantReply = message.GetProperty("content").GetString();

                    // Return the assistant reply
                    return Ok(new { reply = assistantReply });
                }
            }
            catch (JsonException)
            {
                // Fall through and return raw response
            }

            return Ok(new { raw = responseString });
        }

        //refresh the server every 5 minutes
        [HttpGet("refresh-page")]
        public IActionResult Refresh()
        {

            _hub.Clients.All.SendAsync("UpdateVisit");
            return Ok();
        }
        
    }
}
