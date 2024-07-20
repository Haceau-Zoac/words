using Catalyst;
using Catalyst.Models;
using HtmlAgilityPack;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

public class Arguments
{
    public Arguments(string[] args)
    {
        Filters = new();
        if (args.Length >= 3)
        {
            for (int i = 3; i <= args.Length; i++)
            {
                if (args[i][0] != '-') continue;
                if (args[i][1] == 'f') Filters = File.ReadAllLines(args[i]).TakeLast(args[i].Length - 2).ToList();
                if (args[i][1] == 's') Sort = true;
            }
        }
    }

    public bool Sort { get; set; }

    public List<string> Filters { get; set; }
}

public class Word
{
    public string? Spelling { get; set; }
    public string? Phonetic { get; set; }
    public string? Translation { get; set; }

    public override string ToString()
    {
        return Spelling + " /" + Phonetic + "/ " + Translation?.Replace("\\n", " ") + '\n';
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("No file input.");
            return;
        }

        Arguments arguments = new(args);

        try
        {
            List<string> content = GetContents(args[0], args[1]);
            Process(ref content);
            content = Lemmatization(content, arguments.Filters);
            if (arguments.Sort) content.Sort();
            File.WriteAllText(".\\processed.txt", string.Join("\n", content));
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return;
        }
    }

    private static List<string> GetContents(string type, string input)
    {
        switch (type)
        {
        case "txt":
            return File.ReadAllText(input).Split(' ').Distinct().ToList();
        case "url":
            return ReadFromUrl(input);
        case "pdf":
            return ConvertPdfToTxt(input).Split(' ').Distinct().ToList();
        }
        throw new Exception("Unknown filetype.");
    }

    private static string ConvertPdfToTxt(string input)
    {
        string result = string.Empty;
        using (PdfReader pdfReader = new(input))
        using (PdfDocument pdfDocument = new(pdfReader))
        for (int page = 1; page <= pdfDocument.GetNumberOfPages(); page++)
        {
            string pageText = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(page));
            result += pageText;
        }
        return result;
    }

    public static List<string> ReadFromUrl(string url)
    {
        var web = new HtmlWeb();
        var document = web.Load(url);
        HashSet<string> list = new();
        foreach (var node in document.DocumentNode.ChildNodes)
        {
            list.Add(node.GetDirectInnerText());
        }
        return list.ToList();
    }

    public static void Process(ref List<string> content)
    {
        List<string> processed = new List<string>();
        foreach (var word in content)
        {
            string buffer = "";
            foreach (var character in word)
            {
                if ((character >= 'a' && character <= 'z')
                    || (character >= 'A' && character <= 'Z')
                    || (character == '-'))
                {
                    buffer += character;
                }
                else if (buffer != "")
                {
                    processed.Add(buffer);
                    buffer = "";
                }
            }
            if (buffer != "")
            {
                processed.Add(buffer);
            }
        }
        content = processed.Distinct().ToList();
    }

    public static List<string> Lemmatization(List<string> content, List<string> filters)
    {
        string contentString = string.Join(' ', content.ToArray()).ToLower();
        HashSet<string> output = new HashSet<string>();

        English.Register();
        var nlp = Pipeline.For(Mosaik.Core.Language.English);
        var doc = new Document(contentString, Mosaik.Core.Language.English);

        nlp.ProcessSingle(doc);
        var tokenList = doc.ToTokenList();
        foreach (var token in tokenList)
        {
            if (!filters.Contains(token.Lemma))
            {
                output.Add(token.Lemma.Trim());
            }
        }
        return output.ToList();
    }
}