using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class Admin : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Newsroom | Dashboard");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--db-accent:" + site.Settings.Accent : string.Empty);
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("db-menu", Auth.Menu("db", "Admin", c, site));
            response.SetElementContents("db-who", Auth.Who("db", c));
            response.SetElementContents("db-demo", Auth.DemoPill("db", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminJs.out();");
            return response;
        }

        private static string Letters(string perm)
        {
            StringBuilder sb = new StringBuilder("<span class=\"db-letters\" title=\"View, Upload, Edit, Delete, Publish\">");
            foreach (char ch in Access.Letters)
            {
                bool on = perm.Contains(ch);
                sb.Append("<i class=\"" + (on ? (ch == 'P' ? "db-p" : "db-y") : string.Empty) + "\">" + ch + "</i>");
            }
            sb.Append("</span>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            response.SetElementContents("db-main", await MainHtml(c, site));
            response.ExecuteScript("AdminJs.loaded();");
            return response;
        }

        private async Task<string> MainHtml(Ctx c, SiteData site)
        {
            Staff u = c.U;
            List<Activity> acts = await Store.LoadActivity(c.Sbx);
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            List<Story> visible = site.Stories.Where(s => Access.Can(u, s.Section, 'V')).ToList();
            List<Story> review = site.Stories.Where(s => s.Status == "review" && Access.Can(u, s.Section, 'P')).OrderBy(s => s.Updated).ToList();
            List<Story> mine = site.Stories.Where(s => s.Author == u.Id && !Access.Live(s)).OrderByDescending(s => s.Updated).ToList();
            int today = visible.Count(s => Access.Live(s) && s.Date > DateTime.UtcNow.AddHours(-24));
            int pending = site.Comments.Count(x => x.Status == "pending" && site.Stories.Any(s => s.Id == x.Story && Access.Can(u, s.Section, 'P')));
            bool publisher = Access.AnyPublish(u, site);
            int held = site.Stories.Count(s => s.Status == "suspended" && Access.Can(u, s.Section, 'P'));
            StringBuilder sb = new StringBuilder("<div class=\"db-ptop\"><div><h1>Welcome, " + Ui.Enc(u.Name.Split(' ')[0]) + "</h1><p>" + Ui.Enc(u.Title) + (u.Admin ? " &middot; full access to every desk" : string.Empty) + "</p></div><div class=\"db-acts\"><a class=\"db-btn db-btn-accent\" href=\"AdminArticles?new=1\">New story</a></div></div>");
            sb.Append("<div class=\"db-stats\">");
            sb.Append("<a class=\"db-stat\" href=\"AdminArticles\"><b>" + today + "</b><span>Published in the last 24 hours</span></a>");
            if (publisher)
            {
                sb.Append("<a class=\"db-stat" + (review.Count > 0 ? " db-hot" : string.Empty) + "\" href=\"AdminArticles?f=review\"><b>" + review.Count + "</b><span>Waiting for your review</span></a>");
                sb.Append("<a class=\"db-stat" + (pending > 0 ? " db-hot" : string.Empty) + "\" href=\"AdminComments\"><b>" + pending + "</b><span>Comments to moderate</span></a>");
            }
            else
            {
                sb.Append("<a class=\"db-stat\" href=\"AdminArticles?f=review\"><b>" + mine.Count(s => s.Status == "review") + "</b><span>Your stories in review</span></a>");
                sb.Append("<a class=\"db-stat\" href=\"AdminArticles?f=draft\"><b>" + mine.Count(s => s.Status == "draft") + "</b><span>Your drafts</span></a>");
            }
            sb.Append(u.Admin ? "<div class=\"db-stat\"><b>" + site.Subscribers.Count + "</b><span>Newsletter sign-ups</span></div>" : "<a class=\"db-stat\" href=\"AdminArticles\"><b>" + visible.Count(Access.Live) + "</b><span>Live stories on your desks</span></a>");
            sb.Append("</div>");
            if (held > 0)
            {
                sb.Append("<div class=\"db-warn-box db-held\"><span><b>" + held + (held == 1 ? " story is" : " stories are") + " suspended</b> and hidden from readers.</span><a href=\"AdminArticles?f=suspended\">Review suspended stories</a></div>");
            }
            sb.Append("<div class=\"db-grid2\"><div>");
            if (publisher)
            {
                sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>Waiting for review</h2><a href=\"AdminArticles?f=review\">All</a></div>");
                if (review.Count == 0)
                {
                    sb.Append("<p class=\"db-none\">Nothing waiting. When a reporter sends a story for review it appears here.</p>");
                }
                foreach (Story s in review.Take(6))
                {
                    string by = staff.TryGetValue(s.Author, out Staff? a) ? a.Name : s.Author;
                    sb.Append("<div class=\"db-row\">" + Ui.Avatar("db", a) + "<div><b>" + Ui.Enc(s.Title) + "</b><small>" + Ui.Enc(Ui.SectionName(site, s.Section)) + " &middot; " + Ui.Enc(by) + " &middot; " + Ui.Time(s.Updated) + "</small></div><a class=\"db-btn db-btn-sm\" href=\"AdminArticles?id=" + Uri.EscapeDataString(s.Id) + "\">Review</a></div>");
                }
                sb.Append("</section>");
            }
            sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>Your drafts and submissions</h2><a href=\"AdminArticles?f=mine\">All</a></div>");
            if (mine.Count == 0)
            {
                sb.Append("<p class=\"db-none\">No drafts. Start one with New story.</p>");
            }
            foreach (Story s in mine.Take(5))
            {
                sb.Append("<div class=\"db-row\"><div><b>" + Ui.Enc(s.Title) + "</b><small>" + Ui.Enc(Ui.SectionName(site, s.Section)) + " &middot; " + Ui.Time(s.Updated) + "</small></div>" + Auth.StatusPill("db", s) + "<a class=\"db-btn db-btn-sm db-btn-ghost\" href=\"AdminArticles?id=" + Uri.EscapeDataString(s.Id) + "\">Open</a></div>");
            }
            sb.Append("</section><section class=\"db-panel\"><div class=\"db-ph\"><h2>Recent activity</h2><a href=\"AdminActivity\">All</a></div>");
            List<Activity> list = acts.Where(x => u.Admin || x.User == u.Id).Take(6).ToList();
            if (list.Count == 0)
            {
                sb.Append("<p class=\"db-none\">No activity yet.</p>");
            }
            foreach (Activity x in list)
            {
                string who = staff.TryGetValue(x.User, out Staff? a) ? a.Name : x.User;
                sb.Append("<div class=\"db-row\"><div><b>" + Ui.Enc(who) + " &middot; " + Ui.Enc(x.Action) + (x.Ok ? string.Empty : " (refused)") + "</b><small>" + Ui.Enc(x.Target) + " &middot; " + Ui.Time(x.When) + "</small></div></div>");
            }
            sb.Append("</section></div><div><section class=\"db-panel\"><h2>Your access</h2>" + (u.Admin ? "<p class=\"db-hint\">Editor-in-chief: every permission on every section, plus users, departments, settings and the front page.</p>" : string.Empty) + "<table class=\"db-acc\">");
            foreach (Desk d in site.Desks.Where(d => !u.Admin))
            {
                List<DeskSection> secs = d.Sections.Where(s => Access.Perm(u, s.Key) != string.Empty).ToList();
                if (secs.Count == 0)
                {
                    continue;
                }
                sb.Append("<tr class=\"db-dk\"><td colspan=\"2\">" + Ui.Enc(d.Name) + (Access.DeskPublisher(u, d) ? " &middot; arranges the " + Ui.Enc(d.Name) + " page" : string.Empty) + "</td></tr>");
                foreach (DeskSection s in secs)
                {
                    sb.Append("<tr><td>" + Ui.Enc(s.Name) + "</td><td>" + Letters(Access.Perm(u, s.Key)) + "</td></tr>");
                }
            }
            sb.Append("</table><div class=\"db-legend\"><span><b>V</b> view</span><span><b>U</b> upload and write</span><span><b>E</b> edit others&rsquo; drafts</span><span><b>D</b> delete drafts</span><span><b>P</b> publish, edit and delete anything, moderate comments</span></div>");
            if (u.Admin || u.FrontPage)
            {
                sb.Append("<p class=\"db-hint\" style=\"margin-top:12px\">You can arrange the front page.</p>");
            }
            sb.Append("</section><section class=\"db-panel\"><h2>Quick links</h2><div class=\"db-quick\"><a class=\"db-btn db-btn-sm db-btn-ghost\" href=\"AdminArticles?new=1\">Write a story</a><a class=\"db-btn db-btn-sm db-btn-ghost\" href=\"AdminMedia\">Media library</a>");
            if (u.Admin || u.FrontPage || site.Desks.Any(d => Access.DeskPublisher(u, d)))
            {
                sb.Append("<a class=\"db-btn db-btn-sm db-btn-ghost\" href=\"AdminPages\">Arrange pages</a>");
            }
            sb.Append("<a class=\"db-btn db-btn-sm db-btn-ghost\" href=\"Home\" target=\"_blank\" rel=\"noopener\">Open the site</a></div></section></div></div>");
            return sb.ToString();
        }
    }
}
