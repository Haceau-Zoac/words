using Catalyst;
using Catalyst.Models;
using HtmlAgilityPack;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using PuppeteerSharp;
using System.Text;

public class Arguments
{
    public Arguments(string[] args)
    {
        Filters = new();

        if (args[0] == "login")
        {
            Login = true;
            return;
        }

        if (args.Length >= 3)
        {
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i][0] != '-') continue;
                if (args[i][1] == 'f') Filters = File.ReadAllLines(args[i][1..]).ToList();
                if (args[i][1] == 's') Sort = true;
                if (args[i][1] == 'd')
                {
                    DictionaryName = args[i][2..];
                    DictionaryDescription = args[i + 1];
                }
            }
        }
    }

    public bool Sort { get; set; }
    public string? DictionaryName { get; set; }
    public string? DictionaryDescription { get; set; }
    public bool Login { get; set; }

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
    public static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Input?");
            return;
        }

        Arguments arguments = new(args);
        if (arguments.Login)
        {
            Lang = new LangEasyLexis();
            await Lang.Login(args[1]);
            return;
        }

        try
        {
            List<string> content = await GetContents(args[0], args[1]);
            Process(ref content);
            content = Lemmatization(content, arguments.Filters);
            if (arguments.Sort) content.Sort();

            string result = string.Join("\n", content);
            if (arguments.DictionaryName != null)
            {
                Lang = new LangEasyLexis(true);
                await Lang.PostDictionary(result, args[0], arguments.DictionaryName, arguments.DictionaryDescription);
            }
            else
            {
                File.WriteAllText(".\\processed.txt", result);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return;
        }
    }

    private static async Task<List<string>> GetContents(string type, string input)
    {
        return type switch
        {
            "txt" => File.ReadAllText(input).Split(' ').Distinct().ToList(),
            "url" => await ReadFromUrl(input),
            "pdf" => ConvertPdfToTxt(input).Split(' ').Distinct().ToList(),
            _ => throw new Exception("Unknown filetype."),
        };
    }

    private static string ConvertPdfToTxt(string input)
    {
        string result = string.Empty;
        using PdfReader pdfReader = new(input);
        using PdfDocument pdfDocument = new(pdfReader);
        for (int page = 1; page <= pdfDocument.GetNumberOfPages(); page++)
        {
            string pageText = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(page));
            result += pageText;
        }
        return result;
    }

    public static async Task<List<string>> ReadFromUrl(string url)
    {
        var browser = Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            ExecutablePath = "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe"
        }).Result;

        IPage page = await browser.NewPageAsync();

        await page.GoToAsync(url);
        Thread.Sleep(1000);
        string content = await page.GetContentAsync();

        var document = new HtmlAgilityPack.HtmlDocument();
        document.LoadHtml(content);
        return ReadChildNodes(document.DocumentNode);
    }

    public static List<string> ReadChildNodes(HtmlNode doc)
    {
        List<string> list = new();
        foreach (var node in doc.ChildNodes)
        {
            if (node.Name != "script" && node.Name != "style")
            {
                if (node.ChildNodes.Count > 0)
                {
                    list.AddRange(ReadChildNodes(node));
                }

                list.Add(node.GetDirectInnerText());
            }
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
    private static LangEasyLexis? Lang { get; set; }
}