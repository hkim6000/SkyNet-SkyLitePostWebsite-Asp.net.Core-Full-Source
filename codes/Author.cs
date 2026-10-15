using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class Author : WebPage
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
            View v = Render(site, Ui.StaffMap(staff), pages, QueryValue("u"), string.Empty, true);
            HtmlDoc.SetTitle((v.Title != string.Empty ? v.Title + " | " : string.Empty) + site.Settings.Name);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", v.Desc != string.Empty ? v.Desc : site.Settings.Tagline);
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_name}", Ui.Enc(site.Settings.Name))
                .Replace("{plhd_tagline}", Ui.Enc(site.Settings.Tagline))
                .Replace("{plhd_theme}", site.Settings.Theme == "dark" ? "dark" : "light")
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--au-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_now}", Ui.Iso(DateTime.UtcNow))
                .Replace("{plhd_today}", DateTime.UtcNow.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture))
                .Replace("{plhd_nav}", Ui.DeskNav("au", site, v.Desk))
                .Replace("{plhd_subnav}", Ui.SubNav("au", site, v.Desk, v.Section))
                .Replace("{plhd_breaking}", Ui.BreakingBar("au", site))
                .Replace("{plhd_drawer}", Ui.Drawer("au", site))
                .Replace("{plhd_footdesks}", Ui.FootDesks("au", site))
                .Replace("{plhd_social}", Social.Html(site.Settings, "au", "foot"))
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
                response.ExecuteScript("AuthorJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            List<Staff> staff = await Store.LoadStaff(sbx);
            Dictionary<string, List<PageModule>> pages = await Store.LoadPages(sbx);
            View v = Render(site, Ui.StaffMap(staff), pages, Ui.Clip(GetDataValue("argA"), 120), Ui.Clip(GetDataValue("argB"), 120), false);
            response.SetElementContents("au-main", v.Main);
            response.SetElementContents("au-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("au-fname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("au-cname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("au-tagline", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("au-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("au-desks", Ui.DeskNav("au", site, v.Desk));
            response.SetElementContents("au-subnav", Ui.SubNav("au", site, v.Desk, v.Section));
            response.SetElementContents("au-breaking", Ui.BreakingBar("au", site));
            response.SetElementContents("au-drawer", Ui.Drawer("au", site));
            response.SetElementContents("au-fdesks", Ui.FootDesks("au", site));
            response.SetElementContents("au-foot-soc", Social.Html(site.Settings, "au", "foot"));
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("AuthorJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("AuthorJs.sandboxed('" + sbx + "');");
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
                response.SetElementContents("au-e-nl-" + mod, "Please enter a valid email address.");
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
            response.ExecuteScript("AuthorJs.subscribed('" + mod + "');");
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

        private View Render(SiteData site, Dictionary<string, Staff> staff, Dictionary<string, List<PageModule>> pages, string id, string b, bool first)
        {
            if (!staff.TryGetValue(id, out Staff? u) || !u.Active)
            {
                return new View { Title = "Not found", Main = NotFound("au", site, staff, "writer") };
            }
            Desk? desk = site.Desks.FirstOrDefault(d => d.Key == u.Dept);
            List<Story> list = Ui.LiveStories(site).Where(s => s.Author == u.Id).Take(30).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"au-wrap\"><div class=\"au-prof\">" + Ui.Avatar("au", u, "lg") + "<div><small>" + Ui.Enc(u.Title) + (desk != null ? " &middot; " + Ui.Enc(desk.Name) : string.Empty) + "</small><h1>" + Ui.Enc(u.Name) + "</h1>" + (u.Bio != string.Empty ? "<p>" + Ui.Enc(u.Bio) + "</p>" : string.Empty) + "</div></div>");
            sb.Append("<section class=\"au-mod\"><div class=\"au-mh\"><h2>Latest from " + Ui.Enc(u.Name.Split(' ')[0]) + "</h2></div>");
            if (list.Count == 0)
            {
                sb.Append("<p class=\"au-empty\">No published stories yet.</p>");
            }
            else
            {
                sb.Append("<ul class=\"au-rows\">");
                foreach (Story s in list)
                {
                    sb.Append(Row("au", site, staff, s));
                }
                sb.Append("</ul>");
            }
            sb.Append("</section></div>");
            return new View { Title = u.Name, Desc = u.Title + ", " + site.Settings.Name, Desk = desk?.Key ?? string.Empty, Main = sb.ToString() };
        }
    }
}
