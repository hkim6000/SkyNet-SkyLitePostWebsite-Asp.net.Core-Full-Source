using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class About : WebPage
    {
        private sealed class View
        {
            public string Title { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
            public string Main { get; set; } = string.Empty;
            public string Desk { get; set; } = string.Empty;
            public string Section { get; set; } = string.Empty;
        }

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            List<Staff> staff = await Store.LoadStaff();
            Dictionary<string, List<PageModule>> pages = await Store.LoadPages();
            View v = Render(site, Ui.StaffMap(staff), pages, string.Empty, string.Empty, true);
            HtmlDoc.SetTitle((v.Title != string.Empty ? v.Title + " | " : string.Empty) + site.Settings.Name);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", v.Desc != string.Empty ? v.Desc : site.Settings.Tagline);
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_name}", Ui.Enc(site.Settings.Name))
                .Replace("{plhd_tagline}", Ui.Enc(site.Settings.Tagline))
                .Replace("{plhd_theme}", site.Settings.Theme == "dark" ? "dark" : "light")
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--ab-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_now}", Ui.Iso(DateTime.UtcNow))
                .Replace("{plhd_today}", DateTime.UtcNow.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture))
                .Replace("{plhd_nav}", Ui.DeskNav("ab", site, v.Desk))
                .Replace("{plhd_subnav}", Ui.SubNav("ab", site, v.Desk, v.Section))
                .Replace("{plhd_breaking}", Ui.BreakingBar("ab", site))
                .Replace("{plhd_drawer}", Ui.Drawer("ab", site))
                .Replace("{plhd_footdesks}", Ui.FootDesks("ab", site))
                .Replace("{plhd_social}", Social.Html(site.Settings, "ab", "foot"))
                .Replace("{plhd_main}", v.Main);
        }

        private static bool SafeColor(string v)
        {
            return Regex.IsMatch(v ?? string.Empty, "^#[0-9a-fA-F]{6}$");
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("AboutJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            List<Staff> staff = await Store.LoadStaff(sbx);
            Dictionary<string, List<PageModule>> pages = await Store.LoadPages(sbx);
            View v = Render(site, Ui.StaffMap(staff), pages, Ui.Clip(GetDataValue("argA"), 120), Ui.Clip(GetDataValue("argB"), 120), false);
            response.SetElementContents("ab-main", v.Main);
            response.SetElementContents("ab-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("ab-fname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("ab-cname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("ab-tagline", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("ab-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("ab-desks", Ui.DeskNav("ab", site, v.Desk));
            response.SetElementContents("ab-subnav", Ui.SubNav("ab", site, v.Desk, v.Section));
            response.SetElementContents("ab-breaking", Ui.BreakingBar("ab", site));
            response.SetElementContents("ab-drawer", Ui.Drawer("ab", site));
            response.SetElementContents("ab-fdesks", Ui.FootDesks("ab", site));
            response.SetElementContents("ab-foot-soc", Social.Html(site.Settings, "ab", "foot"));
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("AboutJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
            return response;
        }

        private static async Task<string> Writable(ApiResponse response, string requested)
        {
            if (!Store.Demo)
            {
                return string.Empty;
            }
            string sbx = Store.Sandbox(requested);
            if (sbx == string.Empty)
            {
                sbx = await Store.NewSandbox();
                response.ExecuteScript("AboutJs.sandboxed('" + sbx + "');");
            }
            return sbx;
        }

        public async Task<ApiResponse> Subscribe()
        {
            ApiResponse response = new ApiResponse();
            string mod = Regex.IsMatch(GetDataValue("mod"), "^[A-Za-z0-9_-]{1,40}$") ? GetDataValue("mod") : string.Empty;
            string mail = Ui.Clip(GetDataValue("mail"), 100);
            if (mod == string.Empty)
            {
                return response;
            }
            if (!IsEmail(mail))
            {
                response.SetElementContents("ab-e-nl-" + mod, "Please enter a valid email address.");
                return response;
            }
            string sbx = await Writable(response, GetDataValue("sbx"));
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(sbx);
                if (!site.Subscribers.Any(s => s.Email.Equals(mail, StringComparison.OrdinalIgnoreCase)) && site.Subscribers.Count < 50000)
                {
                    site.Subscribers.Add(new Subscriber { Email = mail, When = DateTime.UtcNow });
                    await Store.SaveSite(site, sbx);
                }
            }
            response.ExecuteScript("AboutJs.subscribed('" + mod + "');");
            return response;
        }

        private static bool IsEmail(string v)
        {
            if (v.Length < 5 || v.Length > 80 || v.Contains(' '))
            {
                return false;
            }
            int at = v.IndexOf('@');
            int dot = v.LastIndexOf('.');
            return at > 0 && at == v.LastIndexOf('@') && dot > at + 1 && dot < v.Length - 1;
        }

        private static string NotFound(string p, SiteData site, Dictionary<string, Staff> staff, string what)
        {
            PageModule m = new PageModule { Id = "nf", Style = "rundown", Title = "Latest news", Source = "all", Count = 4 };
            return "<div class=\"" + p + "-wrap\"><div class=\"" + p + "-404\"><h1>We couldn&rsquo;t find that " + what + "</h1><p>It may have moved, or it is not published yet.</p><a class=\"" + p + "-btn\" href=\"Home\">Go to the front page</a></div>" + Ui.Module(p, site, staff, m, new HashSet<string>()) + "</div>";
        }

        private static string Row(string p, SiteData site, Dictionary<string, Staff> staff, Story s, bool kicker = true)
        {
            return "<li>" + (Ui.HasPic(s) ? Ui.Img(p, s, "tpic", true) : string.Empty) + "<div>" + (kicker ? Ui.Kicker(p, site, s) : string.Empty) + Ui.Head(p, s, "h3") + "<p class=\"" + p + "-dek\">" + Ui.Enc(s.Dek) + "</p>" + Ui.Meta(p, s, staff) + "</div></li>";
        }

        private View Render(SiteData site, Dictionary<string, Staff> staff, Dictionary<string, List<PageModule>> pages, string a, string b, bool first)
        {
            Settings st = site.Settings;
            StringBuilder sb = new StringBuilder("<div class=\"ab-wrap\"><div class=\"ab-phead\"><h1>About the Post</h1><p>" + Ui.Enc(st.Tagline) + "</p></div><div class=\"ab-about\"><div class=\"ab-about-t\">");
            foreach (string para in st.About.Replace("\r", string.Empty).Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                sb.Append("<p>" + Ui.Enc(para.Trim()) + "</p>");
            }
            sb.Append("</div><div class=\"ab-box\" id=\"contact\"><h2>Contact the newsroom</h2><dl>");
            if (st.Email != string.Empty)
            {
                sb.Append("<div><dt>Email</dt><dd>" + Ui.Enc(st.Email) + "</dd></div>");
            }
            if (st.Phone != string.Empty)
            {
                sb.Append("<div><dt>Phone</dt><dd>" + Ui.Enc(st.Phone) + "</dd></div>");
            }
            if (st.Address != string.Empty)
            {
                sb.Append("<div><dt>Address</dt><dd>" + Ui.Enc(st.Address) + "</dd></div>");
            }
            sb.Append("</dl></div></div><section class=\"ab-mod\"><div class=\"ab-mh\"><h2>The newsroom</h2></div><div class=\"ab-team\">");
            foreach (Desk d in site.Desks)
            {
                foreach (Staff u in staff.Values.Where(x => x.Active && x.Dept == d.Key).OrderByDescending(x => x.Admin).ThenBy(x => x.Name))
                {
                    sb.Append("<a class=\"ab-member\" href=\"Author?u=" + Uri.EscapeDataString(u.Id) + "\">" + Ui.Avatar("ab", u, "md") + "<div><b>" + Ui.Enc(u.Name) + "</b><small>" + Ui.Enc(u.Title) + "</small></div></a>");
                }
            }
            sb.Append("</div></section>");
            sb.Append(Ui.Module("ab", site, staff, new PageModule { Id = "fl", Style = "follow", Title = "Follow the Post", Text = "Breaking news, photos and the day's front page." }, new HashSet<string>()));
            sb.Append("</div>");
            return new View { Title = "About", Desc = "About " + st.Name, Main = sb.ToString() };
        }
    }
}
