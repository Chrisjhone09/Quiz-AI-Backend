namespace back.Models
{
    public class Feedback
    {
        public Guid Id { get; set; }
        public string Name { get; set; } 
        public int? Rate { get; set; }
        public string? Comment { get; set; }
    

    public static string GenerateRandomName()
        {
            var random = new Random();

            return "Student-" + random.Next(100000, 999999).ToString();

        }
    }
}
