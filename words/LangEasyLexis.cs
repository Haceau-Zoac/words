using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HtmlAgilityPack;
using PuppeteerSharp;
using Cookie = System.Net.Cookie;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

public class RecognitionDataBody
{
    [JsonPropertyName("unknowList")]
    public string UnknowList { get; set; }
    [JsonPropertyName("knowList")]
    public string KnowList {  get; set; }
}

public class RecognitionResult
{
    [JsonPropertyName("result_code")]
    public int ResultCode { get; set; }
    [JsonPropertyName("data_kind")]
    public string DataKind { get; set; }
    [JsonPropertyName("data_version")]
    public string DataVersion { get; set; }
    [JsonPropertyName("data_body")]
    public RecognitionDataBody DataBody { get; set; }
}

public class LangEasyLexis
{
    public LangEasyLexis(bool hasCookies = false)
    {
        _handler = new HttpClientHandler();
        _handler.UseCookies = true;
        _handler.CookieContainer = hasCookies ? LoadCookies() : new CookieContainer();

        _client = new HttpClient(_handler);
        _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36 Edg/121.0.0.0");
        _client.DefaultRequestHeaders.Add("Sec-CH-UA", "\"Not A(Brand\";v=\"99\", \"Microsoft Edge\";v=\"121\", \"Chromium\";v=\"121\"");

        _browser = Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            ExecutablePath = "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe"
        }).Result;
    }

    ~LangEasyLexis()
    {
        _client.Dispose();
    }

    public async Task Login(string phone)
    {
        IPage page = await _browser.NewPageAsync();

        await page.GoToAsync("https://bbdc.cn/index");
        Thread.Sleep(1000);
        string content = await page.GetContentAsync();
        string token = GetToken(content);

        Image image = await GetCaptchaImage(token);
        string? captchaCode = null;
        Thread captcha = new(new ThreadStart(() =>
        {
            Application.Run(new CaptchaForm(image));
        }));
        captcha.SetApartmentState(ApartmentState.STA);
        captcha.Start();

        Console.Write("图像验证码：");
        captchaCode = Console.ReadLine();

        await SendMessage(token, phone, captchaCode, page);
        Console.Write("短信验证码：");
        string? messageCaptchaCode = Console.ReadLine();

        var response = await _client.GetAsync($"https://bbdc.cn/phone/code/validate?captchaToken={token}&phone={phone}&verifyCode={captchaCode}&code={messageCaptchaCode}");
        SaveCookies();
    }

    public async Task PostDictionary(string content, string fileName, string dictionaryName, string description)
    {
        string? knowList = (await UploadDictionary(content, fileName)
            ?? throw new Exception("file is invalid."));
        await MakeDictionary(knowList, description, dictionaryName);
    }

    private static string GetToken(string content)
    {
        HtmlDocument document = new();
        document.LoadHtml(content);
        HtmlNode node = document.DocumentNode.SelectSingleNode("//input[@name='captchaToken']");
        return node.Attributes["value"].Value;
    }
    private async Task<string?> UploadDictionary(string content, string fileName)
    {
        using MultipartFormDataContent form = new();
        using StreamContent fileContent = new(new MemoryStream(Encoding.UTF8.GetBytes(content)));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);

        HttpResponseMessage response = await _client.PostAsync("https://bbdc.cn/lexis/book/file/submit", form);
        response.EnsureSuccessStatusCode();

        string responseContent = await response.Content.ReadAsStringAsync();
        RecognitionResult? result = JsonSerializer.Deserialize<RecognitionResult>(responseContent);
        return result?.DataBody.KnowList;
    }

    private async Task MakeDictionary(string wordList, string description, string name)
    {
        Dictionary<string, string> postData = new()
        {
            { "wordList", wordList },
            { "desc", description },
            { "name", name },
            { "exam", "" },
        };

        FormUrlEncodedContent content = new(postData);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue
            ("application/x-www-form-urlencoded");
        HttpResponseMessage response = await _client.PostAsync("https://bbdc.cn/lexis/book/save", content);
        response.EnsureSuccessStatusCode();
    }

    private async Task<Image> GetCaptchaImage(string token)
    {
        HttpResponseMessage response = await _client.GetAsync($"https://bbdc.cn/captcha_code?token={token}");
        response.EnsureSuccessStatusCode();

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        using MemoryStream memoryStream = new(bytes);
        return Image.FromStream(memoryStream);
    }

    private async Task SendMessage(string token, string phone, string? captcha, IPage page)
    {
        var result = await page.GoToAsync($"https://bbdc.cn/phone/code?phone={phone}&captchaToken={token}&verifyCode={captcha}");
        var json = await result.JsonAsync();
        var error_body = json["error_body"];
        if ((error_body != null) && (error_body["user_message"] != null))
        {
            throw new Exception(error_body["user_message"].ToString());
        } 
    }

    private void SaveCookies()
    {
        CookieCollection cookies = _handler.CookieContainer.GetAllCookies();
        string cookieString = string.Empty;
        foreach (Cookie cookie in cookies.Cast<Cookie>())
        {
            cookieString += $"{cookie.Name}={cookie.Value}\n";
        }
        File.WriteAllText("cookies.txt", cookieString);
    }

    private static CookieContainer LoadCookies()
    {
        var cookieContainer = new CookieContainer();
        if (File.Exists("cookies.txt"))
        {
            var uri = new Uri("https://bbdc.cn");
            var lines = File.ReadAllLines("cookies.txt");
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    var cookie = new Cookie(parts[0], parts[1]);
                    cookieContainer.Add(uri, cookie);
                }
            }
        }
        return cookieContainer;
    }

    private IBrowser _browser;
    private HttpClient _client;
    private HttpClientHandler _handler;
}

public class CaptchaForm : Form
{
    public CaptchaForm(Image image)
    {
        Text = "验证码";
        ClientSize = new Size(800, 600); // 窗体大小
        StartPosition = FormStartPosition.CenterScreen; // 窗体居中显示

        // 创建一个 PictureBox 控件来显示图片
        PictureBox pictureBox = new PictureBox
        {
            Image = image,
            SizeMode = PictureBoxSizeMode.Zoom, // 图片缩放模式
            Dock = DockStyle.Fill // 填充整个窗体
        };

        // 将 PictureBox 添加到窗体
        Controls.Add(pictureBox);
    }
}