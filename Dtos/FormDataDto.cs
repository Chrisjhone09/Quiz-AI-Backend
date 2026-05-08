namespace back.Dtos
{
    public class FormDataDto
    {
        public required IFormFile File { get; set; }
        public required QuestionTypes QuestionTypes{ get; set; }
        public required int NumQuestions { get; set; }
        public required string Difficulty { get; set; }

    }
}
