using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class Section : WebPage
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
            View v = Render(site, Ui.StaffMap(staff), pages, QueryValue("d"), QueryValue("s"), true);
            HtmlDoc.SetTitle((v.Title != string.Empty ? v.Title + " | " : string.Empty) + site.Settings.Name);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", v.Desc != string.Empty ? v.Desc : site.Settings.Tagline);
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_name}", Ui.Enc(site.Settings.Name))
                .Replace("{plhd_tagline}", Ui.Enc(site.Settings.Tagline))
                .Replace("{plhd_theme}", site.Settings.Theme == "dark" ? "dark" : "light")
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--sc-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_now}", Ui.Iso(DateTime.UtcNow))
                .Replace("{plhd_today}", DateTime.UtcNow.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture))
                .Replace("{plhd_nav}", Ui.DeskNav("sc", site, v.Desk))
                .Replace("{plhd_subnav}", Ui.SubNav("sc", site, v.Desk, v.Section))
                .Replace("{plhd_breaking}", Ui.BreakingBar("sc", site))
                .Replace("{plhd_drawer}", Ui.Drawer("sc", site))
                .Replace("{plhd_footdesks}", Ui.FootDesks("sc", site))
                .Replace("{plhd_social}", Social.Html(site.Settings, "sc", "foot"))
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
                response.ExecuteScript("SectionJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            List<Staff> staff = await Store.LoadStaff(sbx);
            Dictionary<string, List<PageModule>> pages = await Store.LoadPages(sbx);
            View v = Render(site, Ui.StaffMap(staff), pages, Ui.Clip(GetDataValue("argA"), 120), Ui.Clip(GetDataValue("argB"), 120), false);
            response.SetElementContents("sc-main", v.Main);
            response.SetElementContents("sc-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("sc-fname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("sc-cname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("sc-tagline", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("sc-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("sc-desks", Ui.DeskNav("sc", site, v.Desk));
            response.SetElementContents("sc-subnav", Ui.SubNav("sc", site, v.Desk, v.Section));
            response.SetElementContents("sc-breaking", Ui.BreakingBar("sc", site));
            response.SetElementContents("sc-drawer", Ui.Drawer("sc", site));
            response.SetElementContents("sc-fdesks", Ui.FootDesks("sc", site));
            response.SetElementContents("sc-foot-soc", Social.Html(site.Settings, "sc", "foot"));
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("SectionJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("SectionJs.sandboxed('" + sbx + "');");
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
                response.SetElementContents("sc-e-nl-" + mod, "Please enter a valid email address.");
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
            response.ExecuteScript("SectionJs.subscribed('" + mod + "');");
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

        private const int PageSize = 10;

        private View Render(SiteData site, Dictionary<string, Staff> staff, Dictionary<string, List<PageModule>> pages, string d, string s, bool first)
        {
            Desk? desk = site.Desks.FirstOrDefault(x => x.Key == d);
            if (desk != null)
            {
                List<PageModule> mods = pages.TryGetValue(desk.Key, out List<PageModule>? l) ? l : new List<PageModule>();
                if (mods.Count == 0)
                {
                    mods = new List<PageModule> { new PageModule { Id = "d1", Style = "lead", Source = "d:" + desk.Key, Count = 4 }, new PageModule { Id = "d2", Style = "rundown", Title = "Latest", Source = "d:" + desk.Key, Count = 8 } };
                }
                string head = "<div class=\"sc-phead\"><h1>" + Ui.Enc(desk.Name) + "</h1>" + (desk.Blurb != string.Empty ? "<p>" + Ui.Enc(desk.Blurb) + "</p>" : string.Empty) + "</div>";
                return new View { Title = desk.Name, Desc = desk.Blurb, Desk = desk.Key, Main = "<div class=\"sc-wrap\">" + head + Ui.Modules("sc", site, staff, mods) + "</div>" };
            }
            DeskSection? sec = Ui.Section(site, s);
            if (sec == null)
            {
                return new View { Title = "Not found", Main = NotFound("sc", site, staff, "section") };
            }
            Desk owner = Ui.DeskOf(site, sec.Key)!;
            List<Story> all = Ui.LiveStories(site).Where(x => x.Section == sec.Key).ToList();
            HashSet<string> used = new HashSet<string>();
            string top = all.Count > 0 ? Ui.Module("sc", site, staff, new PageModule { Id = "top", Style = "lead", Source = "s:" + sec.Key, Count = Math.Min(4, all.Count) }, used) : "<p class=\"sc-empty\">No stories in this section yet.</p>";
            List<Story> rest = all.Where(x => !used.Contains(x.Id)).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"sc-wrap\"><div class=\"sc-phead\"><a class=\"sc-kick\" href=\"Section?d=" + Uri.EscapeDataString(owner.Key) + "\">" + Ui.Enc(owner.Name) + "</a><h1>" + Ui.Enc(sec.Name) + "</h1></div>" + top);
            if (rest.Count > 0)
            {
                sb.Append("<section class=\"sc-mod\"><div class=\"sc-mh\"><h2>Latest in " + Ui.Enc(sec.Name) + "</h2></div><ul class=\"sc-rows\" id=\"sc-list\">" + Rows(site, staff, rest, 0) + "</ul><div id=\"sc-lm\">" + MoreButton(rest.Count, 0) + "</div></section>");
            }
            sb.Append("</div>");
            return new View { Title = sec.Name, Desc = owner.Name + ": " + sec.Name, Desk = owner.Key, Section = sec.Key, Main = sb.ToString() };
        }

        private static string Rows(SiteData site, Dictionary<string, Staff> staff, List<Story> list, int from)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Story x in list.Skip(from).Take(PageSize))
            {
                sb.Append(Row("sc", site, staff, x, false));
            }
            return sb.ToString();
        }

        private static string MoreButton(int total, int from)
        {
            int next = from + PageSize;
            return next < total ? "<div class=\"sc-loadmore\"><button type=\"button\" class=\"sc-btn sc-btn-ghost\" onclick=\"SectionJs.more(" + next + ")\">Load more</button></div>" : string.Empty;
        }

        public async Task<ApiResponse> More()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            SiteData site = await Store.LoadSite(sbx);
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(sbx));
            DeskSection? sec = Ui.Section(site, Ui.Clip(GetDataValue("argB"), 60));
            int from = int.TryParse(GetDataValue("from"), out int f) ? Math.Clamp(f, 0, 100000) : 0;
            if (sec == null)
            {
                return response;
            }
            HashSet<string> used = new HashSet<string>();
            Ui.Pick(site, new PageModule { Style = "lead", Source = "s:" + sec.Key, Count = 4 }, used);
            List<Story> rest = Ui.LiveStories(site).Where(x => x.Section == sec.Key && !used.Contains(x.Id)).ToList();
            response.ExecuteScript("document.getElementById('sc-list').insertAdjacentHTML('beforeend'," + System.Text.Json.JsonSerializer.Serialize(Rows(site, staff, rest, from)) + ");");
            response.SetElementContents("sc-lm", MoreButton(rest.Count, from));
            response.ExecuteScript("SectionJs.appended();");
            return response;
        }
    }
}
