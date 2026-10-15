using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class Home : WebPage
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
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--hm-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_now}", Ui.Iso(DateTime.UtcNow))
                .Replace("{plhd_today}", DateTime.UtcNow.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture))
                .Replace("{plhd_nav}", Ui.DeskNav("hm", site, v.Desk))
                .Replace("{plhd_subnav}", Ui.SubNav("hm", site, v.Desk, v.Section))
                .Replace("{plhd_breaking}", Ui.BreakingBar("hm", site))
                .Replace("{plhd_drawer}", Ui.Drawer("hm", site))
                .Replace("{plhd_footdesks}", Ui.FootDesks("hm", site))
                .Replace("{plhd_social}", Social.Html(site.Settings, "hm", "foot"))
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
                response.ExecuteScript("HomeJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            List<Staff> staff = await Store.LoadStaff(sbx);
            Dictionary<string, List<PageModule>> pages = await Store.LoadPages(sbx);
            View v = Render(site, Ui.StaffMap(staff), pages, Ui.Clip(GetDataValue("argA"), 120), Ui.Clip(GetDataValue("argB"), 120), false);
            response.SetElementContents("hm-main", v.Main);
            response.SetElementContents("hm-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("hm-fname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("hm-cname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("hm-tagline", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("hm-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("hm-desks", Ui.DeskNav("hm", site, v.Desk));
            response.SetElementContents("hm-subnav", Ui.SubNav("hm", site, v.Desk, v.Section));
            response.SetElementContents("hm-breaking", Ui.BreakingBar("hm", site));
            response.SetElementContents("hm-drawer", Ui.Drawer("hm", site));
            response.SetElementContents("hm-fdesks", Ui.FootDesks("hm", site));
            response.SetElementContents("hm-foot-soc", Social.Html(site.Settings, "hm", "foot"));
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("HomeJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("HomeJs.sandboxed('" + sbx + "');");
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
                response.SetElementContents("hm-e-nl-" + mod, "Please enter a valid email address.");
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
            response.ExecuteScript("HomeJs.subscribed('" + mod + "');");
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
            List<PageModule> mods = pages.TryGetValue("home", out List<PageModule>? l) ? l : new List<PageModule>();
            if (mods.Count == 0)
            {
                mods = new List<PageModule> { new PageModule { Id = "d1", Style = "lead", Source = "all", Count = 4 }, new PageModule { Id = "d2", Style = "rundown", Title = "Latest", Source = "all", Count = 8 } };
            }
            return new View { Desk = "home", Main = "<h1 class=\"hm-sr\">" + Ui.Enc(site.Settings.Name) + " front page</h1><div class=\"hm-wrap\">" + Ui.Modules("hm", site, staff, mods) + "</div>" };
        }
    }
}
