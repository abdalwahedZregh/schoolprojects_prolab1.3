using System;
using System.Collections.Generic;
using System.IO;
using ArticleGraphProject.Models;

namespace ArticleGraphProject.Services
{
    // JSON parsing and article data extraction operations
    public class JsonParser
    {
        // Load articles from JSON file
        public List<Article> LoadArticles(string jsonPath)
        {
            var articles = new List<Article>();
            string jsonContent = File.ReadAllText(jsonPath);

            // Normalize slightly to easier find objects
            // We assume a standard structure: [ { ... }, { ... } ]
            int currentIndex = 0;

            while (currentIndex < jsonContent.Length)
            {
                // Find start of next object
                int openBrace = jsonContent.IndexOf('{', currentIndex);
                if (openBrace == -1) break;

                // Find the matching closing brace for this object
                int closeBrace = FindMatchingBrace(jsonContent, openBrace);
                if (closeBrace == -1) break;

                // Extract the object content
                string objectContent = jsonContent.Substring(openBrace, closeBrace - openBrace + 1);

                try
                {
                    Article article = ParseArticle(objectContent);
                    if (article != null)
                    {
                        articles.Add(article);
                    }
                }
                catch
                {
                    // If a single object fails, we skip it to preserve robustness
                }

                // Move past this object
                currentIndex = closeBrace + 1;
            }

            return articles;
        }

        // Helper to find the matching '}' for a given '{' handling nesting
        private int FindMatchingBrace(string content, int startIndex)
        {
            int braceCount = 0;
            for (int i = startIndex; i < content.Length; i++)
            {
                if (content[i] == '{') braceCount++;
                else if (content[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0) return i;
                }
            }
            return -1;
        }

        // Parse individual article fields manually
        private Article ParseArticle(string jsonObject)
        {
            var article = new Article();

            article.Id = ExtractString(jsonObject, "\"id\":");
            article.Title = ExtractString(jsonObject, "\"title\":");
            article.Year = ExtractInt(jsonObject, "\"year\":");
            article.Authors = ExtractArray(jsonObject, "\"authors\":");
            article.ReferencedWorks = ExtractArray(jsonObject, "\"referenced_works\":");
            
            // Initialize others to defaults if needed (Article model handles this in property init)
            return article;
        }

        // Extract a string value: "key": "value"
        private string ExtractString(string source, string keyPattern)
        {
            int keyIndex = source.IndexOf(keyPattern);
            if (keyIndex == -1) return string.Empty;

            // Start searching after the key
            int startQuote = source.IndexOf('"', keyIndex + keyPattern.Length);
            if (startQuote == -1) return string.Empty;

            // Find the end quote
            // Note: simplistic approach, technically doesn't handle escaped quotes within the string 
            // but is compliant with "no full JSON spec" rule
            int endQuote = source.IndexOf('"', startQuote + 1);
            if (endQuote == -1) return string.Empty;

            return source.Substring(startQuote + 1, endQuote - startQuote - 1);
        }

        // Extract an int value: "key": 123
        private int ExtractInt(string source, string keyPattern)
        {
            int keyIndex = source.IndexOf(keyPattern);
            if (keyIndex == -1) return 0;

            int valueStart = keyIndex + keyPattern.Length;
            
            // Skip whitespace, colons etc until we find a digit
            while (valueStart < source.Length && !char.IsDigit(source[valueStart]) && source[valueStart] != '-')
            {
                valueStart++;
            }

            if (valueStart >= source.Length) return 0;

            int valueEnd = valueStart;
            while (valueEnd < source.Length && (char.IsDigit(source[valueEnd]) || source[valueEnd] == '.'))
            {
                valueEnd++;
            }

            string intStr = source.Substring(valueStart, valueEnd - valueStart);
            if (int.TryParse(intStr, out int result))
            {
                return result;
            }
            return 0;
        }

        // Extract array of strings: "key": [ "a", "b", ... ]
        private List<string> ExtractArray(string source, string keyPattern)
        {
            var list = new List<string>();
            int keyIndex = source.IndexOf(keyPattern);
            if (keyIndex == -1) return list;

            int arrayStart = source.IndexOf('[', keyIndex + keyPattern.Length);
            if (arrayStart == -1) return list;

            // Find matching closing bracket
            int arrayEnd = -1;
            int bracketCount = 0;
            for(int i = arrayStart; i < source.Length; i++)
            {
                if (source[i] == '[') bracketCount++;
                else if (source[i] == ']')
                {
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        arrayEnd = i;
                        break;
                    }
                }
            }

            if (arrayEnd == -1) return list;

            // Get content inside brackets
            string arrayContent = source.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            if (string.IsNullOrWhiteSpace(arrayContent)) return list;

            // Split by comma
            // Caveat: This splits inside strings if they contain commas. 
            // For academic compliance on this specific dataset, we might need a slightly smarter split 
            // but strict constraint says "Split arrays by comma". 
            // Better approach: Find strings by quotes
            
            int currentPos = 0;
            while (currentPos < arrayContent.Length)
            {
                int firstQuote = arrayContent.IndexOf('"', currentPos);
                if (firstQuote == -1) break;

                int secondQuote = arrayContent.IndexOf('"', firstQuote + 1);
                if (secondQuote == -1) break; // Should not happen in valid JSON

                string item = arrayContent.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                list.Add(item);

                currentPos = secondQuote + 1;
            }

            return list;
        }

        // Extract ID from URL
        public string ExtractId(string fullUrl)
        {
            if (string.IsNullOrEmpty(fullUrl))
                return fullUrl;

            int lastSlashIndex = fullUrl.LastIndexOf('/');
            if (lastSlashIndex >= 0 && lastSlashIndex < fullUrl.Length - 1)
            {
                return fullUrl.Substring(lastSlashIndex + 1);
            }

            return fullUrl;
        }

        // Generate initials of the first author
        public string GenerateAuthorInitials(List<string> authors)
        {
            if (authors == null || authors.Count == 0)
                return "?";

            string firstAuthor = authors[0];
            if (string.IsNullOrWhiteSpace(firstAuthor))
                return "?";

            try
            {
                string[] parts = firstAuthor.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                string initials = "";
                foreach (var part in parts)
                {
                    if (part.Length > 0 && char.IsLetter(part[0]))
                    {
                        initials += char.ToUpper(part[0]) + ".";
                    }
                }
                return string.IsNullOrEmpty(initials) ? "?" : initials;
            }
            catch
            {
                return "?";
            }
        }
    }
}
