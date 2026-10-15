using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class AdminArticles : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Newsroom | Stories");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--ed-accent:" + site.Settings.Accent : string.Empty);
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ed-menu", Auth.Menu("ed", "AdminArticles", c, site));
            response.SetElementContents("ed-who", Auth.Who("ed", c));
            response.SetElementContents("ed-demo", Auth.DemoPill("ed", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminArticlesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminArticlesJs.out();");
            return response;
        }

        private static string Letters(string perm)
        {
            StringBuilder sb = new StringBuilder("<span class=\"ed-letters\" title=\"View, Upload, Edit, Delete, Publish\">");
            foreach (char ch in Access.Letters)
            {
                bool on = perm.Contains(ch);
                sb.Append("<i class=\"" + (on ? (ch == 'P' ? "ed-p" : "ed-y") : string.Empty) + "\">" + ch + "</i>");
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
                response.ExecuteScript("AdminArticlesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            string open = Ui.Clip(GetDataValue("open"), 120);
            Story? s = open != string.Empty ? site.Stories.FirstOrDefault(x => x.Id == open) : null;
            if (s != null && Access.Can(c.U, s.Section, 'V'))
            {
                response.SetElementContents("ed-main", await Editor(c, site, s));
                response.SetElementContents("ed-mbody", Picker(site, "photo"));
                response.ExecuteScript("AdminArticlesJs.setWhen();AdminArticlesJs.status();AdminArticlesJs.loaded();");
                return response;
            }
            if (GetDataValue("fresh") == "1")
            {
                if (Access.Sections(c.U, site, 'U').Count == 0)
                {
                    return await Refuse(response, c, "create", "new story", "You don't have upload rights on any section.");
                }
                response.SetElementContents("ed-main", await Editor(c, site, null));
                response.SetElementContents("ed-mbody", Picker(site, "photo"));
                response.ExecuteScript("AdminArticlesJs.status();AdminArticlesJs.loaded();document.getElementById('ed-title').focus();");
                return response;
            }
            string f = Filters.Contains(GetDataValue("filter")) ? GetDataValue("filter") : "all";
            string sec = Ui.Clip(GetDataValue("secf"), 60);
            string term = Ui.Clip(GetDataValue("term"), 60);
            (string sk, string sd) = SortOf(GetDataValue("sk"), GetDataValue("sd"));
            string fr = GetDataValue("frontf") == "yes" || GetDataValue("frontf") == "no" ? GetDataValue("frontf") : string.Empty;
            (string rows, string pager) = await Rows(c, site, f, sec, term, GetDataValue("pg"), sk, sd, fr);
            response.SetElementContents("ed-main", ListPage(c, site, f, sec, term, sk, sd, fr));
            response.SetElementContents("ed-list", rows);
            response.SetElementContents("ed-pager", pager);
            response.ExecuteScript("AdminArticlesJs.loaded();");
            return response;
        }

        private static readonly string[] Filters = { "all", "mine", "draft", "review", "scheduled", "published", "suspended" };

        private static readonly string[] SortKeys = { "title", "section", "author", "status", "published", "updated" };

        private static (string Key, string Dir) SortOf(string k, string d)
        {
            if (!SortKeys.Contains(k))
            {
                return (string.Empty, string.Empty);
            }
            return (k, d == "asc" || d == "desc" ? d : (k == "updated" || k == "published" ? "desc" : "asc"));
        }

        private static string Head(string sk, string sd)
        {
            (string k, string n, string cls)[] cols = { ("title", "Story", string.Empty), ("section", "Section", "ed-c-sec"), ("author", "Author", "ed-c-au"), ("status", "Status", string.Empty), ("published", "DateTime", "ed-c-pt"), ("updated", "Updated", "ed-c-dt") };
            StringBuilder sb = new StringBuilder();
            foreach ((string k, string n, string cls) in cols)
            {
                bool on = k == sk;
                sb.Append("<button type=\"button\" class=\"ed-sh" + (cls != string.Empty ? " " + cls : string.Empty) + (on ? " ed-on" : string.Empty) + "\" onclick=\"AdminArticlesJs.sort('" + k + "')\" aria-label=\"Sort by " + n + "\">" + n + (on ? "<i>" + (sd == "asc" ? "&#9650;" : "&#9660;") + "</i>" : string.Empty) + "</button>");
            }
            sb.Append("<span></span>");
            return sb.ToString();
        }

        private static string SortBox(string sk, string sd)
        {
            (string v, string n)[] opts = { (string.Empty, "Sort: review first, then newest"), ("published:desc", "Newest published"), ("published:asc", "Oldest published"), ("updated:desc", "Newest updated"), ("updated:asc", "Oldest updated"), ("title:asc", "Title A\u2013Z"), ("title:desc", "Title Z\u2013A"), ("section:asc", "Section A\u2013Z"), ("author:asc", "Author A\u2013Z"), ("status:asc", "Status") };
            string cur = sk == string.Empty ? string.Empty : sk + ":" + sd;
            StringBuilder sb = new StringBuilder("<select aria-label=\"Sort by\" onchange=\"AdminArticlesJs.sortSel(this.value)\">");
            foreach ((string v, string n) in opts)
            {
                sb.Append("<option value=\"" + v + "\"" + (v == cur ? " selected" : string.Empty) + ">" + n + "</option>");
            }
            if (cur != string.Empty && !opts.Any(o => o.v == cur))
            {
                sb.Append("<option value=\"" + cur + "\" selected>" + Ui.Enc(sk) + (sd == "asc" ? " \u25B2" : " \u25BC") + "</option>");
            }
            sb.Append("</select>");
            return sb.ToString();
        }

        private static string ListPage(Ctx c, SiteData site, string f, string sec, string term, string sk, string sd, string fr)
        {
            f = Filters.Contains(f) ? f : "all";
            StringBuilder sb = new StringBuilder("<div class=\"ed-ptop\"><div><h1>Stories</h1><p>Stories on the desks you can see.</p></div><div class=\"ed-acts\">");
            if (Access.Sections(c.U, site, 'U').Count > 0)
            {
                sb.Append("<button type=\"button\" class=\"ed-btn ed-btn-accent\" onclick=\"AdminArticlesJs.edit('')\">New story</button>");
            }
            sb.Append("</div></div><div class=\"ed-filters\"><div class=\"ed-chips\" id=\"ed-chips\">" + Chips(c, site, f) + "</div><div class=\"ed-right\"><select id=\"ed-secf\" aria-label=\"Section\" onchange=\"AdminArticlesJs.list()\"><option value=\"\">All sections</option>");
            foreach (Desk d in site.Desks)
            {
                List<DeskSection> secs = d.Sections.Where(s => Access.Can(c.U, s.Key, 'V')).ToList();
                if (secs.Count == 0)
                {
                    continue;
                }
                sb.Append("<optgroup label=\"" + Ui.Attr(d.Name) + "\">");
                foreach (DeskSection s in secs)
                {
                    sb.Append("<option value=\"" + Ui.Attr(s.Key) + "\"" + (s.Key == sec ? " selected" : string.Empty) + ">" + Ui.Enc(s.Name) + "</option>");
                }
                sb.Append("</optgroup>");
            }
            sb.Append("</select><input type=\"search\" id=\"ed-q\" placeholder=\"Search titles &amp; sections\" maxlength=\"60\" oninput=\"AdminArticlesJs.search()\" aria-label=\"Search titles and sections\" value=\"" + Ui.Attr(term) + "\"><select id=\"ed-frf\" aria-label=\"Front page\" onchange=\"AdminArticlesJs.list(1)\"><option value=\"\">All stories</option><option value=\"yes\"" + (fr == "yes" ? " selected" : string.Empty) + ">&#9733; Front-page picks</option><option value=\"no\"" + (fr == "no" ? " selected" : string.Empty) + ">Not on front page</option></select><span class=\"ed-sortm\" id=\"ed-sortbox\">" + SortBox(sk, sd) + "</span><input type=\"hidden\" id=\"ed-sk\" value=\"" + sk + "\"><input type=\"hidden\" id=\"ed-sd\" value=\"" + sd + "\"></div><input type=\"hidden\" id=\"ed-fv\" value=\"" + f + "\"></div>");
            sb.Append("<div class=\"ed-tbl\"><div class=\"ed-tr ed-th\" id=\"ed-th\">" + Head(sk, sd) + "</div><div id=\"ed-list\"></div></div><div id=\"ed-pager\"></div>");
            return sb.ToString();
        }

        private static string Chips(Ctx c, SiteData site, string f)
        {
            List<Story> vis = site.Stories.Where(s => Access.Can(c.U, s.Section, 'V')).ToList();
            (string k, string n, int cnt)[] items =
            {
                ("all", "All", vis.Count),
                ("mine", "Mine", vis.Count(s => s.Author == c.U.Id)),
                ("draft", "Drafts", vis.Count(s => s.Status == "draft")),
                ("review", "Ready for review", vis.Count(s => s.Status == "review")),
                ("scheduled", "Scheduled", vis.Count(s => s.Status == "scheduled" && s.Date > DateTime.UtcNow)),
                ("published", "Published", vis.Count(Access.Live)),
                ("suspended", "Suspended", vis.Count(s => s.Status == "suspended")),
            };
            StringBuilder sb = new StringBuilder();
            foreach ((string k, string n, int cnt) in items)
            {
                sb.Append("<button type=\"button\" class=\"ed-chip" + (k == f ? " ed-act" : string.Empty) + "\" onclick=\"AdminArticlesJs.filter('" + k + "')\">" + n + "<em>" + cnt + "</em></button>");
            }
            return sb.ToString();
        }

        private const int PageSize = 25;

        private static int Rank(Story s)
        {
            return s.Status switch { "review" => 0, "draft" => 1, "scheduled" => Access.Live(s) ? 3 : 2, "suspended" => 4, _ => 3 };
        }

        private static string PubCell(Story s)
        {
            if (s.Status == "scheduled" && s.Date > DateTime.UtcNow)
            {
                return "<i>&#9201; </i>" + Stamp(s.Date);
            }
            return Access.Live(s) || s.Status == "suspended" ? Stamp(s.Date) : "&mdash;";
        }

        private static string Stamp(DateTime d)
        {
            return "<time datetime=\"" + Ui.Iso(d) + "\" data-t=\"pt\">" + d.ToUniversalTime().ToString("d MMM, HH:mm 'UTC'", CultureInfo.InvariantCulture) + "</time>";
        }

        private async Task<(string Rows, string Pager)> Rows(Ctx c, SiteData site, string f, string sec, string term, string pg, string sk, string sd, string fr)
        {
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            IEnumerable<Story> q = site.Stories.Where(s => Access.Can(c.U, s.Section, 'V'));
            q = f switch
            {
                "mine" => q.Where(s => s.Author == c.U.Id),
                "draft" => q.Where(s => s.Status == "draft"),
                "review" => q.Where(s => s.Status == "review"),
                "scheduled" => q.Where(s => s.Status == "scheduled" && s.Date > DateTime.UtcNow),
                "published" => q.Where(Access.Live),
                "suspended" => q.Where(s => s.Status == "suspended"),
                _ => q
            };
            if (sec != string.Empty)
            {
                q = q.Where(s => s.Section == sec);
            }
            if (term != string.Empty)
            {
                q = q.Where(s => s.Title.Contains(term, StringComparison.OrdinalIgnoreCase) || Ui.SectionName(site, s.Section).Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            if (fr == "yes")
            {
                q = q.Where(s => s.Front);
            }
            else if (fr == "no")
            {
                q = q.Where(s => !s.Front);
            }
            IOrderedEnumerable<Story> o = sk switch
            {
                "title" => q.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase),
                "section" => q.OrderBy(s => Ui.SectionName(site, s.Section), StringComparer.OrdinalIgnoreCase),
                "author" => q.OrderBy(s => staff.TryGetValue(s.Author, out Staff? a) ? a.Name : s.Author, StringComparer.OrdinalIgnoreCase),
                "status" => q.OrderBy(Rank),
                "updated" => q.OrderBy(s => s.Updated),
                _ => q.OrderByDescending(s => s.Status == "review").ThenByDescending(s => s.Updated)
            };
            List<Story> all = (sk == string.Empty ? o : sd == "desc" ? o.Reverse() : o.ThenByDescending(s => s.Updated)).ToList();
            if (sk == "published")
            {
                List<Story> dated = q.Where(s => Access.Live(s) || s.Status == "scheduled" || s.Status == "suspended").OrderBy(s => s.Date).ToList();
                if (sd == "desc")
                {
                    dated.Reverse();
                }
                all = dated.Concat(q.Where(s => !Access.Live(s) && s.Status != "scheduled" && s.Status != "suspended").OrderByDescending(s => s.Updated)).ToList();
            }
            if (all.Count == 0)
            {
                return ("<div class=\"ed-tr\"><span class=\"ed-none\">No stories match.</span></div>", string.Empty);
            }
            int page = Ui.PageOf(pg, PageSize, all.Count);
            List<Story> list = all.Skip((page - 1) * PageSize).Take(PageSize).ToList();
            StringBuilder sb = new StringBuilder();
            foreach (Story s in list)
            {
                string by = staff.TryGetValue(s.Author, out Staff? a) ? a.Name : s.Author;
                bool can = Access.CanEdit(c.U, s);
                string flags = (s.Breaking ? "<i>Breaking</i>" : string.Empty) + (s.Front ? "<i class=\"ed-ff\">&#9733; Front</i>" : string.Empty) + (s.Video != string.Empty ? "<i class=\"ed-fp\">Video</i>" : string.Empty);
                sb.Append("<div class=\"ed-tr\"><div>" + (flags != string.Empty ? "<span class=\"ed-flags\">" + flags + "</span>" : string.Empty) + "<b>" + Ui.Enc(s.Title) + "</b><small>" + Ui.Enc(s.Dek) + "</small></div><span class=\"ed-c-sec\">" + Ui.Enc(Ui.SectionName(site, s.Section)) + "</span><span class=\"ed-c-au\">" + Ui.Enc(by) + "</span>" + Auth.StatusPill("ed", s) + "<span class=\"ed-c-pt\">" + PubCell(s) + "</span><span class=\"ed-c-dt\">" + Ui.Time(s.Updated) + "</span><button type=\"button\" class=\"ed-btn ed-btn-sm" + (can ? string.Empty : " ed-btn-ghost") + "\" onclick=\"AdminArticlesJs.edit('" + Ui.Attr(s.Id) + "')\">" + (can ? "Edit" : "View") + "</button></div>");
            }
            return (sb.ToString(), Ui.Pager("ed", page, PageSize, all.Count, "story", "stories", "AdminArticlesJs.list"));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminArticlesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string f = Filters.Contains(GetDataValue("filter")) ? GetDataValue("filter") : "all";
            string sec = Ui.Clip(GetDataValue("secf"), 60);
            (string sk, string sd) = SortOf(GetDataValue("sk"), GetDataValue("sd"));
            string fr = GetDataValue("frontf") == "yes" || GetDataValue("frontf") == "no" ? GetDataValue("frontf") : string.Empty;
            (string rows, string pager) = await Rows(c, site, f, sec, Ui.Clip(GetDataValue("term"), 60), GetDataValue("pg"), sk, sd, fr);
            response.SetElementContents("ed-list", rows);
            response.SetElementContents("ed-pager", pager);
            response.SetElementContents("ed-th", Head(sk, sd));
            response.SetElementContents("ed-sortbox", SortBox(sk, sd));
            response.SetElementContents("ed-chips", Chips(c, site, f));
            response.ExecuteScript("AdminArticlesJs.listed();");
            return response;
        }

        private const int PickSize = 24;

        private static string Picker(SiteData site, string kind)
        {
            (string grid, string pager) = PickGrid(site, string.Empty, "1", kind);
            string noun = kind == "video" ? "video" : "photo";
            return "<h2>Choose a " + noun + "</h2><input type=\"hidden\" id=\"ed-pkind\" value=\"" + noun + "\"><div class=\"ed-pf\"><input type=\"search\" id=\"ed-pq\" maxlength=\"60\" placeholder=\"Search " + noun + "s\" aria-label=\"Search " + noun + "s\" oninput=\"AdminArticlesJs.pickSearch()\"></div><div class=\"ed-pick\" id=\"ed-pickgrid\">" + grid + "</div><div id=\"ed-ppager\">" + pager + "</div>";
        }

        private static (string Grid, string Pager) PickGrid(SiteData site, string q, string pg, string kind)
        {
            string k = kind == "video" ? "video" : "photo";
            List<MediaItem> all = site.Media.Where(x => x.Kind == k && (q == string.Empty || x.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Credit.Contains(q, StringComparison.OrdinalIgnoreCase))).OrderByDescending(x => x.When).ToList();
            if (all.Count == 0)
            {
                return ("<p class=\"ed-none\">No " + k + "s match.</p>", string.Empty);
            }
            int page = Ui.PageOf(pg, PickSize, all.Count);
            StringBuilder sb = new StringBuilder();
            foreach (MediaItem m in all.Skip((page - 1) * PickSize).Take(PickSize))
            {
                if (k == "video")
                {
                    sb.Append("<button type=\"button\" onclick=\"AdminArticlesJs.pickVid('" + Ui.Attr(m.Full) + "','" + Ui.Attr(m.Thumb) + "','" + Ui.Attr(Ui.Js(m.Title)) + "')\"><span class=\"ed-pt\"><img src=\"" + Ui.Attr(m.Thumb) + "\" alt=\"\" loading=\"lazy\"><span class=\"ed-play\"></span></span><span>" + Ui.Enc(m.Title) + "</span></button>");
                }
                else
                {
                    sb.Append("<button type=\"button\" onclick=\"AdminArticlesJs.pick('" + Ui.Attr(m.Full) + "','" + Ui.Attr(Ui.Js(m.Title)) + "','" + Ui.Attr(m.Thumb) + "')\"><img src=\"" + Ui.Attr(m.Thumb) + "\" alt=\"\" loading=\"lazy\"><span>" + Ui.Enc(m.Title) + "</span></button>");
                }
            }
            return (sb.ToString(), Ui.Pager("ed", page, PickSize, all.Count, k, k + "s", "AdminArticlesJs.pickPage"));
        }

        public async Task<ApiResponse> PickList()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminArticlesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            (string grid, string pager) = PickGrid(site, Ui.Clip(GetDataValue("pq"), 60).Trim(), GetDataValue("pg"), GetDataValue("pkind"));
            response.SetElementContents("ed-pickgrid", grid);
            response.SetElementContents("ed-ppager", pager);
            return response;
        }

        public async Task<ApiResponse> ChooseMedia()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminArticlesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            response.SetElementContents("ed-mbody", Picker(site, GetDataValue("pkind")));
            response.ExecuteScript("AdminArticlesJs.openModal();");
            return response;
        }

        private async Task<string> Editor(Ctx c, SiteData site, Story? s)
        {
            bool isNew = s == null;
            Story st = s ?? new Story { Status = "draft", Author = c.U.Id, Section = Access.Sections(c.U, site, 'U').FirstOrDefault()?.Key ?? string.Empty };
            bool can = isNew || Access.CanEdit(c.U, st);
            bool pub = Access.Can(c.U, st.Section, 'P');
            bool held = !isNew && st.Status == "suspended";
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            string by = staff.TryGetValue(st.Author, out Staff? a) ? a.Name : st.Author;
            string dis = can ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder("<div class=\"ed-ebar\"><a class=\"ed-back\" href=\"#\" onclick=\"AdminArticlesJs.backToList();return false;\"><span class=\"ed-arr\">&larr;</span>All stories</a><div class=\"ed-acts\">");
            if (!isNew && Access.Live(st))
            {
                sb.Append("<a class=\"ed-btn ed-btn-sm ed-btn-ghost\" href=\"" + Ui.Link(st) + "\" target=\"_blank\" rel=\"noopener\">View on site</a>");
            }
            sb.Append("</div></div>");
            if (!can)
            {
                sb.Append("<div class=\"ed-info-box\">You can read this story but not change it. " + (held ? "This story is suspended and hidden from readers. Only an editor with Publish rights on " + Ui.Enc(Ui.SectionName(site, st.Section)) + " can change it or put it back." : Access.Live(st) ? "Live stories can only be changed by an editor with Publish rights on " + Ui.Enc(Ui.SectionName(site, st.Section)) + "." : "Ask an editor with Edit rights.") + "</div>");
            }
            else if (held)
            {
                sb.Append("<div class=\"ed-warn-box\"><b>Suspended.</b> This story is hidden from readers. Choose Published to put it back on the site with its original date.</div>");
            }
            else if (st.Status == "review" && pub)
            {
                sb.Append("<div class=\"ed-warn-box\"><b>Ready for review.</b> " + Ui.Enc(by) + " sent this story for review. Publish it, schedule it, or send it back as a draft.</div>");
            }
            else if (st.Status == "review" && !pub)
            {
                sb.Append("<div class=\"ed-info-box\">This story is waiting for an editor to publish it. You can still make changes.</div>");
            }
            sb.Append("<div class=\"ed-ed\"><div><input type=\"hidden\" id=\"ed-sid\" value=\"" + Ui.Attr(isNew ? string.Empty : st.Id) + "\">");
            sb.Append("<label class=\"ed-sr\" for=\"ed-title\">Headline</label><textarea class=\"ed-etitle\" id=\"ed-title\" rows=\"2\" maxlength=\"140\" placeholder=\"Headline\"" + dis + ">" + Ui.Enc(st.Title) + "</textarea><em class=\"ed-err\" id=\"ed-e-title\"></em>");
            sb.Append("<label class=\"ed-sr\" for=\"ed-dek\">Standfirst</label><textarea class=\"ed-edek\" id=\"ed-dek\" rows=\"2\" maxlength=\"240\" placeholder=\"Standfirst: one or two sentences under the headline\"" + dis + ">" + Ui.Enc(st.Dek) + "</textarea>");
            if (can)
            {
                sb.Append("<div class=\"ed-tools\" role=\"toolbar\" aria-label=\"Formatting\"><button type=\"button\" onclick=\"AdminArticlesJs.block('p')\" title=\"Paragraph\">&para;</button><button type=\"button\" onclick=\"AdminArticlesJs.block('h2')\" title=\"Heading\">H2</button><button type=\"button\" onclick=\"AdminArticlesJs.block('h3')\" title=\"Subheading\">H3</button><i></i><button type=\"button\" onclick=\"AdminArticlesJs.cmd('bold')\" title=\"Bold\"><b>B</b></button><button type=\"button\" onclick=\"AdminArticlesJs.cmd('italic')\" title=\"Italic\"><em>I</em></button><button type=\"button\" onclick=\"AdminArticlesJs.block('blockquote')\" title=\"Quote\">&ldquo;&rdquo;</button><button type=\"button\" onclick=\"AdminArticlesJs.cmd('insertUnorderedList')\" title=\"List\">&bull; List</button><i></i><button type=\"button\" onclick=\"AdminArticlesJs.link()\" title=\"Link\">Link</button><button type=\"button\" onclick=\"AdminArticlesJs.pickInline()\" title=\"Photo\">Photo</button></div>");
            }
            sb.Append("<div class=\"ed-body\" id=\"ed-body\" contenteditable=\"" + (can ? "true" : "false") + "\" role=\"textbox\" aria-multiline=\"true\" aria-label=\"Story text\"" + (can ? string.Empty : " style=\"border-radius:8px;margin-top:18px\"") + ">" + (st.Body != string.Empty ? st.Body : "<p><br></p>") + "</div><em class=\"ed-err\" id=\"ed-e-body\"></em></div>");
            sb.Append("<div class=\"ed-sideb\"><section class=\"ed-panel\"><h2>Publishing</h2><div class=\"ed-form2\">");
            if (!isNew)
            {
                sb.Append("<div>" + Auth.StatusPill("ed", st) + " <span class=\"ed-saved\">by " + Ui.Enc(by) + " &middot; updated " + Ui.Time(st.Updated) + "</span></div>");
            }
            sb.Append("<label class=\"ed-field\" for=\"ed-status\"><span>Status</span><select id=\"ed-status\" onchange=\"AdminArticlesJs.status()\"" + dis + ">");
            List<(string k, string n)> opts = new List<(string, string)> { ("draft", "Draft"), ("review", "Ready for review") };
            if (pub || (isNew && Access.AnyPublish(c.U, site)))
            {
                opts.Add(("scheduled", "Scheduled"));
                opts.Add(("published", "Published"));
            }
            if (held || (pub && !isNew && Access.Live(st)))
            {
                opts.Add(("suspended", "Suspended"));
            }
            string cur = st.Status == "scheduled" && st.Date <= DateTime.UtcNow ? "published" : st.Status;
            foreach ((string k, string n) in opts)
            {
                sb.Append("<option value=\"" + k + "\"" + (k == cur ? " selected" : string.Empty) + ">" + n + "</option>");
            }
            sb.Append("</select><em class=\"ed-err\" id=\"ed-e-status\"></em></label>");
            sb.Append("<label class=\"ed-field ed-when\" id=\"ed-whenbox\" for=\"ed-when\"><span>Publish at (your time)</span><input type=\"datetime-local\" id=\"ed-when\" data-iso=\"" + (st.Status == "scheduled" ? Ui.Iso(st.Date) : string.Empty) + "\"" + dis + "><em class=\"ed-err\" id=\"ed-e-when\"></em></label>");
            if (pub || (isNew && Access.AnyPublish(c.U, site)))
            {
                sb.Append("<label class=\"ed-chk\"><input type=\"checkbox\" id=\"ed-breaking\"" + (st.Breaking ? " checked" : string.Empty) + dis + "> Breaking news banner</label>");
            }
            if (c.U.Admin || c.U.FrontPage)
            {
                sb.Append("<label class=\"ed-chk\"><input type=\"checkbox\" id=\"ed-front\"" + (st.Front ? " checked" : string.Empty) + dis + "> Front-page pick</label>");
            }
            sb.Append("<label class=\"ed-chk\"><input type=\"checkbox\" id=\"ed-comments\"" + (st.AllowComments ? " checked" : string.Empty) + dis + "> Allow comments</label>");
            if (can)
            {
                sb.Append("<div class=\"ed-wf\">");
                if (pub)
                {
                    sb.Append("<button type=\"button\" class=\"ed-btn ed-btn-accent ed-btn-wide\" onclick=\"AdminArticlesJs.save()\">Save</button>");
                    if (!Access.Live(st))
                    {
                        sb.Append("<button type=\"button\" class=\"ed-btn ed-btn-ghost ed-btn-wide\" onclick=\"AdminArticlesJs.save('published')\">" + (held ? "Put back on the site" : "Publish now") + "</button>");
                    }
                }
                else
                {
                    sb.Append("<button type=\"button\" class=\"ed-btn ed-btn-ghost ed-btn-wide\" onclick=\"AdminArticlesJs.save('draft')\">Save draft</button><button type=\"button\" class=\"ed-btn ed-btn-accent ed-btn-wide\" onclick=\"AdminArticlesJs.save('review')\">Send for review</button>");
                    if (isNew)
                    {
                        sb.Append("<p class=\"ed-hint\">Publishing needs Publish rights on the section. Your editor will see the story in their review list.</p>");
                    }
                }
                if (!isNew && Access.CanDelete(c.U, st))
                {
                    sb.Append("<button type=\"button\" class=\"ed-btn ed-btn-danger ed-btn-wide ed-btn-sm\" onclick=\"AdminArticlesJs.del()\">Delete story</button>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div></section><section class=\"ed-panel\"><h2>Section</h2><label class=\"ed-field\" for=\"ed-section\"><span class=\"ed-sr\">Section</span><select id=\"ed-section\"" + dis + ">");
            foreach (Desk d in site.Desks)
            {
                List<DeskSection> secs = d.Sections.Where(x => x.Key == st.Section || (can && Access.Can(c.U, x.Key, 'U'))).ToList();
                if (secs.Count == 0)
                {
                    continue;
                }
                sb.Append("<optgroup label=\"" + Ui.Attr(d.Name) + "\">");
                foreach (DeskSection x in secs)
                {
                    sb.Append("<option value=\"" + Ui.Attr(x.Key) + "\"" + (x.Key == st.Section ? " selected" : string.Empty) + ">" + Ui.Enc(x.Name) + "</option>");
                }
                sb.Append("</optgroup>");
            }
            sb.Append("</select><em class=\"ed-err\" id=\"ed-e-section\"></em></label></section>");
            sb.Append("<section class=\"ed-panel\"><h2>Photo</h2><div class=\"ed-form2\"><div class=\"ed-cover\" id=\"ed-cover\">" + (st.Image != string.Empty ? "<img src=\"" + Ui.Attr(Ui.Thumb(st.Image)) + "\" alt=\"\">" : "No photo") + "</div><input type=\"hidden\" id=\"ed-image\" value=\"" + Ui.Attr(st.Image) + "\">");
            if (can)
            {
                sb.Append("<div class=\"ed-row-btns\"><button type=\"button\" class=\"ed-btn ed-btn-sm ed-btn-ghost\" onclick=\"AdminArticlesJs.pickCover()\">Choose photo</button><button type=\"button\" class=\"ed-btn ed-btn-sm ed-btn-ghost\" onclick=\"AdminArticlesJs.noCover()\">Remove</button></div>");
            }
            sb.Append("<label class=\"ed-field\" for=\"ed-caption\"><span>Caption</span><input type=\"text\" id=\"ed-caption\" maxlength=\"160\" value=\"" + Ui.Attr(st.Caption) + "\"" + dis + "></label></div></section>");
            sb.Append("<section class=\"ed-panel\"><h2>Video</h2><div class=\"ed-form2\"><div class=\"ed-cover\" id=\"ed-vcover\">" + (st.Video != string.Empty ? "<img src=\"" + Ui.Attr(st.VideoPoster) + "\" alt=\"\"><span class=\"ed-play\"></span>" : "No video") + "</div><input type=\"hidden\" id=\"ed-video\" value=\"" + Ui.Attr(st.Video) + "\">");
            if (can)
            {
                sb.Append("<div class=\"ed-row-btns\"><button type=\"button\" class=\"ed-btn ed-btn-sm ed-btn-ghost\" onclick=\"AdminArticlesJs.pickVideo()\">Choose video</button><button type=\"button\" class=\"ed-btn ed-btn-sm ed-btn-ghost\" onclick=\"AdminArticlesJs.noVideo()\">Remove</button></div>");
            }
            sb.Append("<label class=\"ed-field\" for=\"ed-vcaption\"><span>Video caption</span><input type=\"text\" id=\"ed-vcaption\" maxlength=\"160\" value=\"" + Ui.Attr(st.VideoCaption) + "\"" + dis + "></label><p class=\"ed-hint\">With a photo, the video plays at the end of the article. Without a photo, the video leads the story.</p></div></section>");
            sb.Append("<section class=\"ed-panel\"><h2>Tags</h2><label class=\"ed-field\" for=\"ed-tags\"><span class=\"ed-sr\">Tags</span><input type=\"text\" id=\"ed-tags\" maxlength=\"120\" value=\"" + Ui.Attr(string.Join(", ", st.Tags)) + "\" placeholder=\"housing, waterfront\"" + dis + "><small class=\"ed-hint\">Separate with commas</small></label></section></div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminArticlesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string id = Ui.Clip(GetDataValue("sid"), 120);
            Story? s = id != string.Empty ? site.Stories.FirstOrDefault(x => x.Id == id) : null;
            if (s == null && id != string.Empty)
            {
                response.ExecuteScript("AdminArticlesJs.toast('That story no longer exists.');");
                return response;
            }
            if (s == null && Access.Sections(c.U, site, 'U').Count == 0)
            {
                return await Refuse(response, c, "create", "new story", "You don't have upload rights on any section.");
            }
            if (s != null && !Access.Can(c.U, s.Section, 'V'))
            {
                return await Refuse(response, c, "view", s.Id, "You don't have access to that section.");
            }
            response.SetElementContents("ed-main", await Editor(c, site, s));
            response.SetElementContents("ed-mbody", Picker(site, "photo"));
            response.ExecuteScript("AdminArticlesJs.setWhen();AdminArticlesJs.status();AdminArticlesJs.opened('" + Ui.Js(s?.Id ?? string.Empty) + "');" + (s == null ? "document.getElementById('ed-title').focus();" : string.Empty));
            return response;
        }

        private static string Slug(string title, IEnumerable<string> taken)
        {
            string b = Regex.Replace(title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (b.Length > 70)
            {
                b = b.Substring(0, 70).Trim('-');
            }
            if (b == string.Empty)
            {
                b = "story";
            }
            HashSet<string> set = new HashSet<string>(taken);
            string s = b;
            int n = 2;
            while (set.Contains(s))
            {
                s = b + "-" + n++;
            }
            return s;
        }

        private static readonly string[] Statuses = { "draft", "review", "scheduled", "published", "suspended" };

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminArticlesJs.expired();");
                return response;
            }
            string id = Ui.Clip(GetDataValue("sid"), 120);
            string title = Regex.Replace(Ui.Clip(GetDataValue("title"), 200), "\\s+", " ");
            string dek = Regex.Replace(Ui.Clip(GetDataValue("dek"), 400), "\\s+", " ");
            string body = Access.Sanitize(GetDataValue("body").Length > 200000 ? string.Empty : GetDataValue("body"));
            string section = Ui.Clip(GetDataValue("section"), 60);
            string image = Ui.Clip(GetDataValue("image"), 200);
            string caption = Ui.Clip(GetDataValue("caption"), 160);
            string video = Ui.Clip(GetDataValue("video"), 200);
            string vcaption = Ui.Clip(GetDataValue("vcaption"), 160);
            string status = Statuses.Contains(GetDataValue("status")) ? GetDataValue("status") : "draft";
            List<string> tags = GetDataValue("tags").Split(',').Select(t => Regex.Replace(t.Trim().ToLowerInvariant(), "[^a-z0-9 &-]", string.Empty)).Where(t => t.Length > 0 && t.Length <= 30).Distinct().Take(8).ToList();
            string eTitle = title.Length < 5 ? "Write a headline (at least 5 characters)." : title.Length > 140 ? "Keep the headline under 140 characters." : string.Empty;
            string eBody = Regex.Replace(body, "<[^>]+>", string.Empty).Trim().Length < 20 && (status == "published" || status == "scheduled" || status == "review") ? "Write at least a sentence or two before sending it on." : string.Empty;
            response.SetElementContents("ed-e-title", eTitle);
            response.SetElementContents("ed-e-body", eBody);
            response.SetElementContents("ed-e-when", string.Empty);
            response.SetElementContents("ed-e-section", string.Empty);
            if (eTitle + eBody != string.Empty)
            {
                return response;
            }
            DateTime when = DateTime.MinValue;
            if (status == "scheduled")
            {
                if (!DateTime.TryParse(GetDataValue("when"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out when) || when <= DateTime.UtcNow.AddMinutes(1))
                {
                    response.SetElementContents("ed-e-when", "Pick a time in the future.");
                    return response;
                }
                if (when > DateTime.UtcNow.AddDays(366))
                {
                    response.SetElementContents("ed-e-when", "Pick a time within the next year.");
                    return response;
                }
            }
            if (image != string.Empty && !Access.SafeImage(image))
            {
                image = string.Empty;
            }
            string action;
            Story? s;
            string refuse = string.Empty;
            string refuseAction = "save";
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                if (Ui.Section(site, section) == null)
                {
                    response.SetElementContents("ed-e-section", "Choose a section.");
                    return response;
                }
                s = id != string.Empty ? site.Stories.FirstOrDefault(x => x.Id == id) : null;
                if (id != string.Empty && s == null)
                {
                    response.ExecuteScript("AdminArticlesJs.toast('That story no longer exists.');");
                    return response;
                }
                bool isNew = s == null;
                bool wasLive = s != null && (Access.Live(s) || s.Status == "scheduled");
                bool held = s != null && s.Status == "suspended";
                if (isNew && !Access.Can(c.U, section, 'U'))
                {
                    refuse = "You can't write stories in " + Ui.SectionName(site, section) + ".";
                    refuseAction = "create";
                }
                else if (!isNew && !Access.CanEdit(c.U, s!))
                {
                    refuse = held ? "Only an editor with Publish rights can change a suspended story." : wasLive ? "Only an editor with Publish rights can change a live story." : "You can't edit this story.";
                }
                else if (!isNew && s!.Section != section && !Access.Can(c.U, section, 'U'))
                {
                    refuse = "You can't move stories into " + Ui.SectionName(site, section) + ".";
                }
                else if ((status == "published" || status == "scheduled") && !Access.Can(c.U, section, 'P'))
                {
                    refuse = "Publishing needs Publish rights on " + Ui.SectionName(site, section) + ". Send it for review instead.";
                    refuseAction = status == "published" ? "publish" : "schedule";
                }
                else if (status == "suspended" && !Access.Can(c.U, section, 'P'))
                {
                    refuse = "Suspending a story needs Publish rights on " + Ui.SectionName(site, section) + ".";
                    refuseAction = "suspend";
                }
                else if (status == "suspended" && (isNew || !(Access.Live(s!) || held)))
                {
                    refuse = "Only a live story can be suspended.";
                    refuseAction = "suspend";
                }
                if (refuse == string.Empty)
                {
                    bool pub = Access.Can(c.U, section, 'P');
                    if (isNew)
                    {
                        s = new Story { Id = Slug(title, site.Stories.Select(x => x.Id)), Author = c.U.Id, Date = DateTime.UtcNow };
                        site.Stories.Insert(0, s);
                    }
                    Story t = s!;
                    string before = t.Status;
                    t.Title = title;
                    t.Dek = dek;
                    t.Body = body;
                    t.Section = section;
                    t.Image = image;
                    t.Caption = caption;
                    MediaItem? vm = video != string.Empty && Access.SafeVideo(video) ? site.Media.FirstOrDefault(x => x.Kind == "video" && x.Full == video) : null;
                    t.Video = vm?.Full ?? string.Empty;
                    t.VideoPoster = vm?.Thumb ?? string.Empty;
                    t.VideoCaption = vm != null ? vcaption : string.Empty;
                    t.Tags = tags;
                    t.AllowComments = GetDataValue("comments") == "1";
                    if (pub)
                    {
                        t.Breaking = GetDataValue("breaking") == "1";
                    }
                    else if (!wasLive)
                    {
                        t.Breaking = false;
                    }
                    if (c.U.Admin || c.U.FrontPage)
                    {
                        t.Front = GetDataValue("front") == "1";
                    }
                    if (status == "published")
                    {
                        if (!Access.Live(t) && before != "suspended")
                        {
                            t.Date = DateTime.UtcNow;
                        }
                    }
                    else if (status == "scheduled")
                    {
                        t.Date = when;
                    }
                    else if (status != "suspended")
                    {
                        t.Date = DateTime.UtcNow;
                    }
                    t.Status = status;
                    t.Updated = DateTime.UtcNow;
                    await Store.SaveSite(site, c.Sbx);
                    action = status == "suspended" && before != "suspended" ? "suspend" : status == "published" && before == "suspended" ? "restore" : status == "published" && before != "published" ? "publish" : status == "scheduled" && before != "scheduled" ? "schedule" : status == "review" && before != "review" ? "submit" : wasLive && status == "draft" ? "unpublish" : isNew ? "create" : "save";
                }
                else
                {
                    action = string.Empty;
                }
            }
            if (refuse != string.Empty)
            {
                return await Refuse(response, c, refuseAction, id != string.Empty ? id : title, refuse);
            }
            await Store.Log(c.Sbx, c.U.Id, action, s!.Id, true);
            SiteData fresh = await Store.LoadSite(c.Sbx);
            Story now = fresh.Stories.First(x => x.Id == s.Id);
            string msg = action switch { "suspend" => "Suspended – hidden from readers", "restore" => "Back on the site", "publish" => "Published", "schedule" => "Scheduled", "submit" => "Sent for review", "unpublish" => "Taken down – now a draft", _ => "Saved" };
            response.SetElementContents("ed-main", await Editor(c, fresh, now));
            Chrome(response, c, fresh);
            response.ExecuteScript("AdminArticlesJs.setWhen();AdminArticlesJs.status();AdminArticlesJs.opened('" + Ui.Js(now.Id) + "');AdminArticlesJs.saved();AdminArticlesJs.toast('" + msg + "');");
            return response;
        }

        public async Task<ApiResponse> Delete()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminArticlesJs.expired();");
                return response;
            }
            string id = Ui.Clip(GetDataValue("sid"), 120);
            bool ok = false;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Story? s = site.Stories.FirstOrDefault(x => x.Id == id);
                if (s != null && Access.CanDelete(c.U, s))
                {
                    site.Stories.Remove(s);
                    site.Comments.RemoveAll(x => x.Story == s.Id);
                    await Store.SaveSite(site, c.Sbx);
                    ok = true;
                }
            }
            if (!ok)
            {
                return await Refuse(response, c, "delete", id, "You can't delete this story.");
            }
            await Store.Log(c.Sbx, c.U.Id, "delete", id, true);
            using (await Store.Hold())
            {
                Dictionary<string, List<PageModule>> pages = await Store.LoadPages(c.Sbx);
                bool changed = false;
                foreach (PageModule m in pages.Values.SelectMany(v => v).Where(m => m.Pin == id))
                {
                    m.Pin = string.Empty;
                    changed = true;
                }
                if (changed)
                {
                    await Store.SavePages(pages, c.Sbx);
                }
            }
            SiteData fresh = await Store.LoadSite(c.Sbx);
            Chrome(response, c, fresh);
            (string rows, string pager) = await Rows(c, fresh, "all", string.Empty, string.Empty, "1", string.Empty, string.Empty, string.Empty);
            response.SetElementContents("ed-main", ListPage(c, fresh, "all", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));
            response.SetElementContents("ed-list", rows);
            response.SetElementContents("ed-pager", pager);
            response.ExecuteScript("AdminArticlesJs.saved();history.replaceState(null,'','AdminArticles');AdminArticlesJs.loaded();AdminArticlesJs.toast('Story deleted');");
            return response;
        }
    }
}
