using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class AdminMedia : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle("Newsroom | Media");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Regex.IsMatch(site.Settings.Accent ?? string.Empty, "^#[0-9a-fA-F]{6}$") ? "--md-accent:" + site.Settings.Accent : string.Empty);
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("md-menu", Auth.Menu("md", "AdminMedia", c, site));
            response.SetElementContents("md-who", Auth.Who("md", c));
            response.SetElementContents("md-demo", Auth.DemoPill("md", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminMediaJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminMediaJs.out();");
            return response;
        }

        private static string Letters(string perm)
        {
            StringBuilder sb = new StringBuilder("<span class=\"md-letters\" title=\"View, Upload, Edit, Delete, Publish\">");
            foreach (char ch in Access.Letters)
            {
                bool on = perm.Contains(ch);
                sb.Append("<i class=\"" + (on ? (ch == 'P' ? "md-p" : "md-y") : string.Empty) + "\">" + ch + "</i>");
            }
            sb.Append("</span>");
            return sb.ToString();
        }

        private static long MaxVideo => Store.Demo ? 20L * 1024 * 1024 : 50L * 1024 * 1024;

        private const int ChunkSize = 2 * 1024 * 1024;

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            response.SetElementContents("md-main", await MainHtml(c, site));
            (string grid, string pager) = await Grid(c, site, string.Empty, string.Empty, string.Empty, "1");
            response.SetElementContents("md-mgrid", grid);
            response.SetElementContents("md-mpager", pager);
            response.ExecuteScript("AdminMediaJs.loaded();");
            return response;
        }

        private async Task<string> MainHtml(Ctx c, SiteData site)
        {
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            StringBuilder sb = new StringBuilder("<div class=\"md-ptop\"><div><h1>Media</h1><p>Photos and videos shared by the whole newsroom.</p></div></div>");
            if (Access.AnyUpload(c.U, site))
            {
                string demo = Store.Demo ? "<p class=\"md-hint\">Demo: uploads stay in your private copy and are removed after an hour.</p>" : string.Empty;
                sb.Append("<details class=\"md-panel md-col\"><summary><b>Upload a photo</b><span>JPG, PNG or WebP &middot; resized to 1600 px</span></summary><div class=\"md-colb\"><div class=\"md-up\"><label class=\"md-drop\" id=\"md-prev\" for=\"md-file\">Choose an image<br>JPG, PNG or WebP<input type=\"file\" id=\"md-file\" accept=\"image/jpeg,image/png,image/webp\" onchange=\"AdminMediaJs.file(this)\"></label><div class=\"md-form2\"><em class=\"md-err\" id=\"md-e-file\"></em><div class=\"md-row2\"><label class=\"md-field\" for=\"md-mtitle\"><span>Title</span><input type=\"text\" id=\"md-mtitle\" maxlength=\"100\"><em class=\"md-err\" id=\"md-e-mtitle\"></em></label><label class=\"md-field\" for=\"md-credit\"><span>Credit</span><input type=\"text\" id=\"md-credit\" maxlength=\"60\" value=\"" + Ui.Attr(c.U.Name) + "\"></label></div><div><button type=\"button\" class=\"md-btn\" onclick=\"AdminMediaJs.upload()\">Upload photo</button></div>" + demo + "</div></div></div></details>");
                sb.Append("<details class=\"md-panel md-col\"><summary><b>Upload a video</b><span>MP4 or WebM &middot; up to " + (MaxVideo / 1048576) + " MB</span></summary><div class=\"md-colb\"><div class=\"md-up\"><label class=\"md-drop\" id=\"md-vprev\" for=\"md-vfile\">Choose a video<br>MP4 or WebM<input type=\"file\" id=\"md-vfile\" accept=\"video/mp4,video/webm\" data-max=\"" + MaxVideo + "\" onchange=\"AdminMediaJs.vpick(this)\"></label><div class=\"md-form2\"><em class=\"md-err\" id=\"md-e-vfile\"></em><div class=\"md-row2\"><label class=\"md-field\" for=\"md-vtitle\"><span>Title</span><input type=\"text\" id=\"md-vtitle\" maxlength=\"100\"><em class=\"md-err\" id=\"md-e-vtitle\"></em></label><label class=\"md-field\" for=\"md-vcredit\"><span>Credit</span><input type=\"text\" id=\"md-vcredit\" maxlength=\"60\" value=\"" + Ui.Attr(c.U.Name) + "\"></label></div><div class=\"md-prog\" id=\"md-vprog\"><i id=\"md-vbar\"></i></div><div><button type=\"button\" class=\"md-btn\" id=\"md-vbtn\" onclick=\"AdminMediaJs.vupload()\">Upload video</button></div><p class=\"md-hint\">A still from the first second becomes the poster.</p>" + demo + "</div></div></div></details>");
            }
            else
            {
                sb.Append("<div class=\"md-info-box\">You can use these photos and videos, but uploading needs Upload rights on at least one section.</div>");
            }
            sb.Append("<div class=\"md-mf\"><div class=\"md-chips md-kinds\"><button type=\"button\" class=\"md-chip md-act\" data-k=\"\" onclick=\"AdminMediaJs.setKind('')\">All</button><button type=\"button\" class=\"md-chip\" data-k=\"photo\" onclick=\"AdminMediaJs.setKind('photo')\">Photos</button><button type=\"button\" class=\"md-chip\" data-k=\"video\" onclick=\"AdminMediaJs.setKind('video')\">Videos</button></div><input type=\"search\" id=\"md-mq\" maxlength=\"60\" placeholder=\"Search title or credit\" aria-label=\"Search media\" oninput=\"AdminMediaJs.search()\"><input type=\"search\" id=\"md-mby\" list=\"md-mpeople\" maxlength=\"60\" placeholder=\"Added by anyone\" aria-label=\"Added by\" autocomplete=\"off\" oninput=\"AdminMediaJs.search()\"><datalist id=\"md-mpeople\">");
            foreach (Staff u in staff.Values.OrderBy(x => x.Name))
            {
                sb.Append("<option value=\"" + Ui.Attr(u.Name) + "\" label=\"" + Ui.Attr(u.Login) + "\"></option>");
            }
            sb.Append("</datalist></div><div class=\"md-mgrid\" id=\"md-mgrid\"></div><div id=\"md-mpager\"></div>");
            return sb.ToString();
        }

        private const int PageSize = 24;

        private static string Dur(int secs)
        {
            return (secs / 60) + ":" + (secs % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private async Task<(string Grid, string Pager)> Grid(Ctx c, SiteData site, string q, string who, string kind, string pg)
        {
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            IEnumerable<MediaItem> found = site.Media;
            if (kind == "photo" || kind == "video")
            {
                found = found.Where(x => x.Kind == kind);
            }
            if (q != string.Empty)
            {
                found = found.Where(x => x.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Credit.Contains(q, StringComparison.OrdinalIgnoreCase));
            }
            if (who != string.Empty)
            {
                HashSet<string> ids = new HashSet<string>(staff.Values.Where(u => u.Name.Contains(who, StringComparison.OrdinalIgnoreCase) || u.Login.Contains(who, StringComparison.OrdinalIgnoreCase)).Select(u => u.Id));
                found = found.Where(x => ids.Contains(x.By));
            }
            List<MediaItem> all = found.OrderByDescending(x => x.When).ToList();
            if (all.Count == 0)
            {
                return ("<p class=\"md-none\">Nothing matches.</p>", string.Empty);
            }
            int page = Ui.PageOf(pg, PageSize, all.Count);
            StringBuilder sb = new StringBuilder();
            foreach (MediaItem m in all.Skip((page - 1) * PageSize).Take(PageSize))
            {
                string by = staff.TryGetValue(m.By, out Staff? a) ? a.Name : m.By;
                bool can = c.U.Admin || (m.By == c.U.Id && Access.AnyUpload(c.U, site));
                bool vid = m.Kind == "video";
                sb.Append("<div class=\"md-mi\"><div class=\"md-mthumb\"><img src=\"" + Ui.Attr(m.Thumb) + "\" alt=\"\" loading=\"lazy\">" + (vid ? "<span class=\"md-play\"></span><span class=\"md-dur\">" + Dur(m.Seconds) + "</span>" : string.Empty) + "</div><div><b>" + Ui.Enc(m.Title) + "</b><small>" + (vid ? "Video &middot; " : string.Empty) + Ui.Enc(m.Credit) + " &middot; added by " + Ui.Enc(by) + "</small></div>");
                if (vid || can)
                {
                    sb.Append("<footer>" + (vid ? "<button type=\"button\" class=\"md-btn md-btn-sm md-btn-ghost\" onclick=\"AdminMediaJs.preview('" + Ui.Attr(m.Full) + "','" + Ui.Attr(m.Thumb) + "')\">Preview</button>" : string.Empty) + (can ? "<button type=\"button\" class=\"md-btn md-btn-sm md-btn-danger\" onclick=\"AdminMediaJs.del('" + Ui.Attr(m.Id) + "',this)\">Delete</button>" : string.Empty) + "</footer>");
                }
                sb.Append("</div>");
            }
            return (sb.ToString(), Ui.Pager("md", page, PageSize, all.Count, "item", "items", "AdminMediaJs.list"));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            (string grid, string pager) = await Grid(c, site, Ui.Clip(GetDataValue("mq"), 60).Trim(), Ui.Clip(GetDataValue("mby"), 60).Trim(), GetDataValue("mkind"), GetDataValue("pg"));
            response.SetElementContents("md-mgrid", grid);
            response.SetElementContents("md-mpager", pager);
            return response;
        }

        private static string ImageExt(byte[] b)
        {
            if (b.Length > 12 && b[0] == 0xFF && b[1] == 0xD8)
            {
                return "jpg";
            }
            if (b.Length > 12 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
            {
                return "png";
            }
            if (b.Length > 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
            {
                return "webp";
            }
            return string.Empty;
        }

        private static byte[]? DataImage(string data, int max)
        {
            Match m = Regex.Match(data ?? string.Empty, "^data:image/(jpeg|png|webp);base64,([A-Za-z0-9+/=]+)$");
            if (!m.Success)
            {
                return null;
            }
            try
            {
                byte[] b = Convert.FromBase64String(m.Groups[2].Value);
                return b.Length <= max && ImageExt(b) != string.Empty ? b : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static string Folder(Ctx c)
        {
            return c.Sbx != string.Empty ? "sandbox/" + c.Sbx : "live";
        }

        private static string Dir(Ctx c)
        {
            return Path.Combine(Store.Root, "images", "uploads", Folder(c).Replace('/', Path.DirectorySeparatorChar));
        }

        private static bool OverQuota(Ctx c, long incoming)
        {
            if (c.Sbx == string.Empty || !Directory.Exists(Dir(c)))
            {
                return false;
            }
            FileInfo[] files = new DirectoryInfo(Dir(c)).GetFiles();
            return files.Length >= 40 || files.Sum(f => f.Length) + incoming > 80L * 1024 * 1024;
        }

        public async Task<ApiResponse> Upload()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            if (!Access.AnyUpload(c.U, site))
            {
                return await Refuse(response, c, "upload", "photo", "Uploading needs Upload rights on at least one section.");
            }
            string title = Regex.Replace(Ui.Clip(GetDataValue("mtitle"), 100), "\\s+", " ");
            string credit = Ui.Clip(GetDataValue("credit"), 60);
            if (title.Length < 2)
            {
                response.SetElementContents("md-e-mtitle", "Give the photo a title.");
                return response;
            }
            byte[]? big = DataImage(GetDataValue("full"), 8 * 1024 * 1024);
            byte[]? small = DataImage(GetDataValue("thumb"), 3 * 1024 * 1024);
            if (big == null || small == null)
            {
                response.SetElementContents("md-e-file", "That file is not a JPG, PNG or WebP image.");
                return response;
            }
            if (OverQuota(c, big.Length + small.Length))
            {
                response.SetElementContents("md-e-file", "Demo limit reached for this copy.");
                return response;
            }
            Directory.CreateDirectory(Dir(c));
            string id = Guid.NewGuid().ToString("N").Substring(0, 16);
            string fe = ImageExt(big);
            string te = ImageExt(small);
            await File.WriteAllBytesAsync(Path.Combine(Dir(c), id + "." + fe), big);
            await File.WriteAllBytesAsync(Path.Combine(Dir(c), id + "-t." + te), small);
            string url = "images/uploads/" + Folder(c) + "/" + id;
            using (await Store.Hold())
            {
                site = await Store.LoadSite(c.Sbx);
                site.Media.Add(new MediaItem { Id = id, Kind = "photo", Full = url + "." + fe, Thumb = url + "-t." + te, Title = title, Credit = credit, By = c.U.Id, When = DateTime.UtcNow });
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "upload", title, true);
            response.ExecuteScript("AdminMediaJs.uploaded();AdminMediaJs.toast('Photo added to the library');");
            return response;
        }

        private static string PartFile(Ctx c, string uid)
        {
            return Path.Combine(Store.Root, "temp", "uploads", (c.Sbx != string.Empty ? c.Sbx : "live") + "-" + uid + ".part");
        }

        public async Task<ApiResponse> Chunk()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            if (!Access.AnyUpload(c.U, site))
            {
                response.ExecuteScript("AdminMediaJs.chunkFail('Uploading needs Upload rights on at least one section.');");
                return await Refuse(response, c, "upload", "video", "Uploading needs Upload rights on at least one section.");
            }
            string uid = Store.CleanId(GetDataValue("uid"));
            int idx = int.TryParse(GetDataValue("idx"), out int i) ? i : -1;
            int total = int.TryParse(GetDataValue("total"), out int t) ? t : 0;
            if (uid == string.Empty || idx < 0 || total < 1 || idx >= total || (long)total * ChunkSize > MaxVideo + ChunkSize)
            {
                response.ExecuteScript("AdminMediaJs.chunkFail('The video is too large or the upload was interrupted.');");
                return response;
            }
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(GetDataValue("part"));
            }
            catch (FormatException)
            {
                response.ExecuteScript("AdminMediaJs.chunkFail('The upload was damaged. Please try again.');");
                return response;
            }
            string file = PartFile(c, uid);
            Directory.CreateDirectory(Path.GetDirectoryName(file) ?? Store.Root);
            if (idx == 0)
            {
                foreach (string old in Directory.GetFiles(Path.GetDirectoryName(file) ?? Store.Root, "*.part"))
                {
                    if (File.GetLastWriteTimeUtc(old) < DateTime.UtcNow.AddHours(-2))
                    {
                        File.Delete(old);
                    }
                }
                if (OverQuota(c, (long)total * ChunkSize))
                {
                    response.ExecuteScript("AdminMediaJs.chunkFail('Demo limit reached for this copy.');");
                    return response;
                }
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            long have = File.Exists(file) ? new FileInfo(file).Length : 0;
            if (have != (long)idx * ChunkSize || bytes.Length == 0 || bytes.Length > ChunkSize || (idx < total - 1 && bytes.Length != ChunkSize) || have + bytes.Length > MaxVideo)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
                response.ExecuteScript("AdminMediaJs.chunkFail('" + (have + bytes.Length > MaxVideo ? "The video must be under " + (MaxVideo / 1048576) + " MB." : "The upload was interrupted. Please try again.") + "');");
                return response;
            }
            using (FileStream fs = new FileStream(file, FileMode.Append, FileAccess.Write))
            {
                await fs.WriteAsync(bytes);
            }
            response.ExecuteScript("AdminMediaJs.chunkOk(" + idx + ");");
            return response;
        }

        public async Task<ApiResponse> VideoDone()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string uid = Store.CleanId(GetDataValue("uid"));
            string file = uid != string.Empty ? PartFile(c, uid) : string.Empty;
            if (!Access.AnyUpload(c.U, site) || file == string.Empty || !File.Exists(file))
            {
                response.ExecuteScript("AdminMediaJs.chunkFail('The upload was interrupted. Please try again.');");
                return response;
            }
            string title = Regex.Replace(Ui.Clip(GetDataValue("vtitle"), 100), "\\s+", " ");
            if (title.Length < 2)
            {
                File.Delete(file);
                response.ExecuteScript("AdminMediaJs.chunkFail('Give the video a title.');");
                return response;
            }
            byte[] head = new byte[12];
            using (FileStream fs = File.OpenRead(file))
            {
                int n = await fs.ReadAsync(head.AsMemory(0, 12));
                if (n < 12)
                {
                    head = Array.Empty<byte>();
                }
            }
            string ext = head.Length == 12 && head[4] == 0x66 && head[5] == 0x74 && head[6] == 0x79 && head[7] == 0x70 ? "mp4" : head.Length == 12 && head[0] == 0x1A && head[1] == 0x45 && head[2] == 0xDF && head[3] == 0xA3 ? "webm" : string.Empty;
            if (ext == string.Empty)
            {
                File.Delete(file);
                response.ExecuteScript("AdminMediaJs.chunkFail('That file is not an MP4 or WebM video.');");
                return response;
            }
            Directory.CreateDirectory(Dir(c));
            string id = Guid.NewGuid().ToString("N").Substring(0, 16);
            File.Move(file, Path.Combine(Dir(c), id + "." + ext));
            string url = "images/uploads/" + Folder(c) + "/" + id;
            string poster = "images/news/thumbs/newsroom.webp";
            byte[]? still = DataImage(GetDataValue("poster"), 3 * 1024 * 1024);
            if (still != null)
            {
                string pe = ImageExt(still);
                await File.WriteAllBytesAsync(Path.Combine(Dir(c), id + "-p." + pe), still);
                poster = url + "-p." + pe;
            }
            int secs = int.TryParse(GetDataValue("secs"), out int sv) ? Math.Clamp(sv, 0, 36000) : 0;
            using (await Store.Hold())
            {
                site = await Store.LoadSite(c.Sbx);
                site.Media.Add(new MediaItem { Id = id, Kind = "video", Full = url + "." + ext, Thumb = poster, Title = title, Credit = Ui.Clip(GetDataValue("vcredit"), 60), Seconds = secs, By = c.U.Id, When = DateTime.UtcNow });
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "upload", "video: " + title, true);
            response.ExecuteScript("AdminMediaJs.vdone();AdminMediaJs.toast('Video added to the library');");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return response;
            }
            string id = Ui.Clip(GetDataValue("mid"), 60);
            MediaItem? gone = null;
            int used = 0;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                MediaItem? m = site.Media.FirstOrDefault(x => x.Id == id);
                if (m != null && (c.U.Admin || (m.By == c.U.Id && Access.AnyUpload(c.U, site))))
                {
                    used = site.Stories.Count(s => s.Image == m.Full || s.Video == m.Full || s.Body.Contains(m.Full, StringComparison.Ordinal));
                    if (used == 0)
                    {
                        site.Media.Remove(m);
                        await Store.SaveSite(site, c.Sbx);
                        gone = m;
                    }
                }
            }
            if (used > 0)
            {
                response.ExecuteScript("AdminMediaJs.toast('Used by " + used + (used == 1 ? " story" : " stories") + " – remove it from them first.');");
                return response;
            }
            if (gone == null)
            {
                return await Refuse(response, c, "media-delete", id, "You can only delete items you uploaded.");
            }
            foreach (string f in new[] { gone.Full, gone.Thumb }.Distinct())
            {
                if (f.StartsWith("images/uploads/", StringComparison.Ordinal) && (Access.SafeImage(f) || Access.SafeVideo(f)))
                {
                    string path = Path.Combine(Store.Root, f.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }
            await Store.Log(c.Sbx, c.U.Id, "media-delete", gone.Title, true);
            response.ExecuteScript("AdminMediaJs.removed();AdminMediaJs.toast('Deleted');");
            return response;
        }
    }
}
