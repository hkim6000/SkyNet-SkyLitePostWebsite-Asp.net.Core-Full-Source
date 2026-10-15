using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class AdminComments : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Newsroom | Comments");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--cm-accent:" + site.Settings.Accent : string.Empty);
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("cm-menu", Auth.Menu("cm", "AdminComments", c, site));
            response.SetElementContents("cm-who", Auth.Who("cm", c));
            response.SetElementContents("cm-demo", Auth.DemoPill("cm", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminCommentsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminCommentsJs.out();");
            return response;
        }

        private static string Letters(string perm)
        {
            StringBuilder sb = new StringBuilder("<span class=\"cm-letters\" title=\"View, Upload, Edit, Delete, Publish\">");
            foreach (char ch in Access.Letters)
            {
                bool on = perm.Contains(ch);
                sb.Append("<i class=\"" + (on ? (ch == 'P' ? "cm-p" : "cm-y") : string.Empty) + "\">" + ch + "</i>");
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
                response.ExecuteScript("AdminCommentsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            StringBuilder sb = new StringBuilder("<div class=\"cm-ptop\"><div><h1>Comments</h1><p>Readers&rsquo; comments appear on the site only after an editor approves them.</p></div></div>");
            if (!Access.AnyPublish(c.U, site))
            {
                response.SetElementContents("cm-main", sb.Append("<div class=\"cm-info-box\">Moderating comments needs Publish rights on a section.</div>").ToString());
                return response;
            }
            sb.Append("<div class=\"cm-chips cm-ftabs\" id=\"cm-ctabs\"></div><div class=\"cm-cf\">");
            sb.Append("<div class=\"cm-range\" role=\"group\" aria-label=\"Period\"><button type=\"button\" class=\"cm-chip\" data-r=\"today\" onclick=\"AdminCommentsJs.setRange('today')\">Today</button><button type=\"button\" class=\"cm-chip\" data-r=\"7\" onclick=\"AdminCommentsJs.setRange('7')\">7 days</button><button type=\"button\" class=\"cm-chip\" data-r=\"30\" onclick=\"AdminCommentsJs.setRange('30')\">30 days</button><button type=\"button\" class=\"cm-chip cm-act\" data-r=\"all\" onclick=\"AdminCommentsJs.setRange('all')\">All</button></div>");
            sb.Append("<div class=\"cm-dates\"><label for=\"cm-from\">From<input type=\"date\" id=\"cm-from\" onchange=\"AdminCommentsJs.custom()\"></label><label for=\"cm-to\">To<input type=\"date\" id=\"cm-to\" onchange=\"AdminCommentsJs.custom()\"></label></div>");
            sb.Append("<label for=\"cm-cq\">Search<input type=\"search\" class=\"cm-q-in\" id=\"cm-cq\" maxlength=\"60\" placeholder=\"Name, comment or story\" autocomplete=\"off\" oninput=\"AdminCommentsJs.search()\"></label>");
            sb.Append("<label for=\"cm-csec\">Section<select id=\"cm-csec\" onchange=\"AdminCommentsJs.list(1)\"><option value=\"\">All my sections</option>");
            foreach (Desk d in site.Desks)
            {
                List<DeskSection> secs = d.Sections.Where(x => Access.Can(c.U, x.Key, 'P')).ToList();
                if (secs.Count == 0)
                {
                    continue;
                }
                sb.Append("<optgroup label=\"" + Ui.Attr(d.Name) + "\">");
                foreach (DeskSection x in secs)
                {
                    sb.Append("<option value=\"" + Ui.Attr(x.Key) + "\">" + Ui.Enc(x.Name) + "</option>");
                }
                sb.Append("</optgroup>");
            }
            sb.Append("</select></label></div><div id=\"cm-clist\"></div><div id=\"cm-cpager\"></div>");
            response.SetElementContents("cm-main", sb.ToString());
            response.ExecuteScript("AdminCommentsJs.loaded();");
            return response;
        }

        private const int PageSize = 20;

        public async Task<ApiResponse> Filter()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminCommentsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string tab = GetDataValue("tab") == "approved" ? "approved" : "pending";
            string q = Ui.Clip(GetDataValue("cq"), 60).Trim();
            string sec = Ui.Clip(GetDataValue("csec"), 60);
            Dictionary<string, Story> stories = site.Stories.Where(s => Access.Can(c.U, s.Section, 'P') && (sec == string.Empty || s.Section == sec)).ToDictionary(s => s.Id, s => s);
            IEnumerable<ReaderComment> found = site.Comments.Where(x => stories.ContainsKey(x.Story));
            if (q != string.Empty)
            {
                found = found.Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Text.Contains(q, StringComparison.OrdinalIgnoreCase) || stories[x.Story].Title.Contains(q, StringComparison.OrdinalIgnoreCase));
            }
            if (DateTime.TryParse(GetDataValue("dfrom"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime from))
            {
                found = found.Where(x => x.When >= from);
            }
            if (DateTime.TryParse(GetDataValue("dto"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime to))
            {
                found = found.Where(x => x.When < to);
            }
            List<ReaderComment> matched = found.ToList();
            int pend = matched.Count(x => x.Status == "pending");
            response.SetElementContents("cm-ctabs", "<button type=\"button\" class=\"cm-chip" + (tab == "pending" ? " cm-act" : string.Empty) + "\" onclick=\"AdminCommentsJs.tab('pending')\">Waiting<em>" + pend + "</em></button><button type=\"button\" class=\"cm-chip" + (tab == "approved" ? " cm-act" : string.Empty) + "\" onclick=\"AdminCommentsJs.tab('approved')\">Approved<em>" + (matched.Count - pend) + "</em></button>");
            List<ReaderComment> all = matched.Where(x => x.Status == tab).OrderByDescending(x => x.When).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), PageSize, all.Count);
            StringBuilder sb = new StringBuilder();
            if (all.Count == 0)
            {
                sb.Append("<p class=\"cm-none\">" + (tab == "pending" ? "No comments waiting here." : "No approved comments here.") + "</p>");
            }
            foreach (ReaderComment x in all.Skip((page - 1) * PageSize).Take(PageSize))
            {
                Story s = stories[x.Story];
                sb.Append("<article class=\"cm-cm\"><header><b>" + Ui.Enc(x.Name) + "</b><small>" + Ui.Time(x.When) + " &middot; on &ldquo;" + Ui.Enc(s.Title) + "&rdquo; (" + Ui.Enc(Ui.SectionName(site, s.Section)) + ")</small>" + (Access.Live(s) ? "<a href=\"" + Ui.Link(s) + "#comments\" target=\"_blank\" rel=\"noopener\">Open story</a>" : string.Empty) + "</header><p>" + Ui.Enc(x.Text) + "</p><footer>" + (x.Status == "pending" ? "<button type=\"button\" class=\"cm-btn cm-btn-sm\" onclick=\"AdminCommentsJs.act('" + Ui.Attr(x.Id) + "','approve')\">Approve</button>" : "<button type=\"button\" class=\"cm-btn cm-btn-sm cm-btn-ghost\" onclick=\"AdminCommentsJs.act('" + Ui.Attr(x.Id) + "','hide')\">Unpublish</button>") + "<button type=\"button\" class=\"cm-btn cm-btn-sm cm-btn-danger\" onclick=\"AdminCommentsJs.act('" + Ui.Attr(x.Id) + "','delete')\">Delete</button></footer></article>");
            }
            response.SetElementContents("cm-clist", sb.ToString());
            response.SetElementContents("cm-cpager", all.Count == 0 ? string.Empty : Ui.Pager("cm", page, PageSize, all.Count, "comment", "comments", "AdminCommentsJs.list"));
            response.ExecuteScript("AdminCommentsJs.listed();");
            return response;
        }

        public async Task<ApiResponse> Moderate()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminCommentsJs.expired();");
                return response;
            }
            string id = Ui.Clip(GetDataValue("cid"), 40);
            string act = GetDataValue("act");
            if (act != "approve" && act != "hide" && act != "delete")
            {
                return response;
            }
            string action = act == "approve" ? "approve" : act == "hide" ? "comment-hide" : "comment-delete";
            bool ok = false;
            string target = id;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                ReaderComment? x = site.Comments.FirstOrDefault(k => k.Id == id);
                Story? s = x != null ? site.Stories.FirstOrDefault(k => k.Id == x.Story) : null;
                if (x != null && s != null && Access.Can(c.U, s.Section, 'P'))
                {
                    target = x.Name + " on " + s.Id;
                    if (act == "delete")
                    {
                        site.Comments.Remove(x);
                    }
                    else
                    {
                        x.Status = act == "approve" ? "approved" : "pending";
                    }
                    await Store.SaveSite(site, c.Sbx);
                    ok = true;
                }
            }
            if (!ok)
            {
                return await Refuse(response, c, action, id, "Moderating needs Publish rights on the story's section.");
            }
            await Store.Log(c.Sbx, c.U.Id, action, target, true);
            SiteData fresh = await Store.LoadSite(c.Sbx);
            Chrome(response, c, fresh);
            response.ExecuteScript("AdminCommentsJs.moderated();AdminCommentsJs.toast('" + (act == "approve" ? "Approved – now on the site" : act == "hide" ? "Moved back to waiting" : "Comment deleted") + "');");
            return response;
        }
    }
}
