using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;

namespace Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UpdateController : ControllerBase
    {
        private readonly string _apkFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        [HttpGet("latest-version")]
        public IActionResult GetLatestVersion()
        {
            var latestFile = Directory.GetFiles(_apkFolderPath)
                .Select(Path.GetFileName)
                .Where(f => f.StartsWith("DSBeauty_"))
                .OrderByDescending(f => f)
                .FirstOrDefault();

            if (latestFile == null)
            {
                return NotFound("Нет доступных APK файлов.");
            }

            var version = latestFile.Split('_')[1].Replace(".apk", "");
            var downloadUrl = Url.Content($"/api/update/download?version={version}");

            return Ok(new
            {
                latest_version = version,
                download_url = downloadUrl
            });
        }

        [HttpGet("download")]
        public IActionResult GetDownloadLink(string version)
        {
            var fileName = $"DSBeauty_{version}.apk";
            var filePath = Path.Combine(_apkFolderPath, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("APK файл не найден.");
            }

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/vnd.android.package-archive", fileName);
        }
    }
}
