using Markdig;

namespace MailDemo
{
    public class MarkdownHelper
    {
        public static string ToHtml(string markdown)
        {
            return Markdown.ToHtml(markdown ?? string.Empty);
        }
    }
}
