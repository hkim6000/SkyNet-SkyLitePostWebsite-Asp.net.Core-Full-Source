using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Models;

namespace SkyLitePost.Core
{
    public static class Social
    {
        public static readonly (string Key, string Name)[] Networks = { ("instagram", "Instagram"), ("youtube", "YouTube"), ("x", "X"), ("facebook", "Facebook"), ("linkedin", "LinkedIn"), ("tiktok", "TikTok"), ("pinterest", "Pinterest"), ("threads", "Threads"), ("behance", "Behance"), ("flickr", "Flickr"), ("vimeo", "Vimeo") };

        public static string Icon(string key)
        {
            return key switch
            {
                "instagram" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"3.5\" width=\"17\" height=\"17\" rx=\"5\"/><circle cx=\"12\" cy=\"12\" r=\"4\"/><circle cx=\"17.2\" cy=\"6.8\" r=\"0.6\" fill=\"currentColor\"/></svg>",
                "youtube" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"currentColor\" aria-hidden=\"true\"><path fill-rule=\"evenodd\" d=\"M4.6 5.6C7 5.2 9.5 5 12 5s5 .2 7.4.6c1.2.2 2.1 1.1 2.3 2.3.2 1.4.3 2.7.3 4.1s-.1 2.7-.3 4.1c-.2 1.2-1.1 2.1-2.3 2.3-2.4.4-4.9.6-7.4.6s-5-.2-7.4-.6c-1.2-.2-2.1-1.1-2.3-2.3C2.1 14.7 2 13.4 2 12s.1-2.7.3-4.1c.2-1.2 1.1-2.1 2.3-2.3zM10 9v6l5.2-3z\"/></svg>",
                "x" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 4l16 16M20 4l-6.4 6.4M10.4 13.6L4 20\" stroke-width=\"2\"/></svg>",
                "facebook" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"3.5\" width=\"17\" height=\"17\" rx=\"4\"/><path d=\"M15.5 8h-1.6c-1.1 0-1.9.9-1.9 2V20.5M9.5 12.5h5.5\"/></svg>",
                "linkedin" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"3.5\" width=\"17\" height=\"17\" rx=\"3\"/><path d=\"M8 10.5V16.5M12 16.5V10.5M12 13.3c0-1.6 1-2.8 2.4-2.8s2.1 1 2.1 2.6v3.4\"/><circle cx=\"8\" cy=\"7.6\" r=\"0.6\" fill=\"currentColor\"/></svg>",
                "tiktok" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M13 4v11a3.5 3.5 0 1 1-3.5-3.5M13 4c.4 2.6 2.2 4.3 5 4.5\"/></svg>",
                "pinterest" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M11 20.3l1.8-7.6M10.2 13.6c-.9-2.6.6-5.6 3.2-5.6 2 0 3.1 1.3 3.1 3 0 2.6-1.6 4.4-3.2 4.4-1 0-1.6-.6-1.4-1.5\"/></svg>",
                "threads" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M16.6 10.8c-.6-2.6-2.4-3.8-4.6-3.8-3 0-5 2.3-5 5.3 0 3.2 2 5.5 5.2 5.5 2.6 0 4.6-1.5 4.6-3.6 0-2-1.8-3-4-3-1.8 0-3 .8-3 2 0 1 .8 1.8 2.1 1.8 2.1 0 3-1.9 2.8-4.6\"/><path d=\"M19.5 7.5C18 4.8 15.4 3.5 12.2 3.5 7.1 3.5 4 7.2 4 12.2s3.1 8.3 8.2 8.3c3.4 0 6-1.6 7.2-4.3\"/></svg>",
                "behance" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M3 6.5h4.5a2.5 2.5 0 0 1 0 5H3zM3 11.5h5.2a2.8 2.8 0 0 1 0 5.6H3zM3 6.5v10.6\"/><path d=\"M14 13.5h7c0-2.3-1.5-4-3.5-4s-3.5 1.7-3.5 3.9 1.5 3.9 3.6 3.9c1.3 0 2.4-.6 3-1.6M15 6.8h5\"/></svg>",
                "flickr" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" aria-hidden=\"true\"><circle cx=\"7.2\" cy=\"12\" r=\"4\" fill=\"currentColor\"/><circle cx=\"16.8\" cy=\"12\" r=\"3.3\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\"/></svg>",
                "vimeo" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M3.5 8.5l1.5-1c1-.7 1.8-.4 2.2.8l1.9 7c.4 1.3 1 1.6 1.9.6 2.2-2.4 4.9-6.6 5.2-8.7.3-2.2-1.6-2.8-3.6-1.7 1.4-3 6.2-4.3 6.3-.2.1 3.3-3.5 9.3-7.2 12.5-1.5 1.3-3 1.2-3.7-.9L6 9.6\"/></svg>",
                _ => string.Empty
            };
        }

        public static bool SafeUrl(string url)
        {
            return url.Length <= 200 && Regex.IsMatch(url, "^https://[A-Za-z0-9.-]+\\.[A-Za-z]{2,}(/[^\\s\"'<>]*)?$");
        }

        public static string Handle(string url)
        {
            string path = Regex.Replace(url, "^https://[^/]+", string.Empty).Trim('/');
            string last = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
            last = Uri.UnescapeDataString(last).TrimStart('@');
            return last == string.Empty ? Regex.Replace(url, "^https://(www\\.)?", string.Empty) : "@" + last;
        }

        public static string Html(Settings st, string p, string mode)
        {
            StringBuilder sb = new StringBuilder();
            foreach ((string k, string name) in Networks)
            {
                SocialLink? l = st.Social.FirstOrDefault(x => x.Key == k && x.Show && SafeUrl(x.Url));
                if (l == null)
                {
                    continue;
                }
                string href = Store.Demo ? "#" : Ui.Attr(l.Url);
                string extra = Store.Demo ? " data-demo=\"1\"" : " target=\"_blank\" rel=\"noopener me\"";
                if (mode == "list")
                {
                    sb.Append("<a class=\"" + p + "-sl\" href=\"" + href + "\"" + extra + ">" + Icon(k) + "<span><b>" + name + "</b><small>" + Ui.Enc(Handle(l.Url)) + "</small></span></a>");
                }
                else
                {
                    sb.Append("<a class=\"" + p + "-soc\" href=\"" + href + "\" aria-label=\"" + name + "\" title=\"" + name + "\"" + extra + ">" + Icon(k) + "</a>");
                }
            }
            return sb.ToString();
        }
    }
}
