using Microsoft.AspNetCore.Mvc;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Net.Http.Headers;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebApplication1.Controllers;

public class VideoController : Controller
{
     private readonly IWebHostEnvironment _env;

 public VideoController(IWebHostEnvironment env)
 {
     _env = env;
 }
 // MIME type checker
 private static readonly string[] ValidVideoMimeTypes = new[]
 {
     "video/mp4",
     "video/x-ms-wmv",
     "video/x-msvideo",
     "video/avi",
     "video/mpeg",
     "video/webm",
     "video/ogg"
 };

 private bool IsValidVideoMimeType(string mimeType)
 {
     return ValidVideoMimeTypes.Contains(mimeType);
 }

 [HttpPost("upload")]
 public async Task<IActionResult> UploadVideo(IFormFile video)
 {
     if (video == null || video.Length == 0)
     {
         return BadRequest("No video selected for upload.");
     }
     // Check MIME type
     if (!IsValidVideoMimeType(video.ContentType))
     {
         return BadRequest("Invalid video file type.");
     }

     var uploadPath = Path.Combine(_env.ContentRootPath, "Uploads", video.FileName);

     try
     {
         // Ensure the uploads directory exists
         var uploadDir = Path.Combine(_env.ContentRootPath, "Uploads");
         if (!Directory.Exists(uploadDir))
         {
             Directory.CreateDirectory(uploadDir);
         }

         using (var stream = new FileStream(uploadPath, FileMode.Create, FileAccess.Write))
         {
             await video.CopyToAsync(stream);
         }
         return Ok(new { path = $"/uploads/{video.FileName}" });
     }
     catch (IOException ex)
     {
         return StatusCode(500, $"Internal server error: {ex.Message}");
     }
 }
 //Download Controll
 [HttpGet]
 [Route("download/{fileName}")]
 public async Task<IActionResult> DownloadVideo(string fileName)
 {
     var uploadsFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
     var filePath = Path.Combine(uploadsFolderPath, fileName);

     if (!System.IO.File.Exists(filePath))
     {
         return NotFound("Video file not found.");
     }

     var memory = new MemoryStream();
     using (var stream = new FileStream(filePath, FileMode.Open))
     {
         await stream.CopyToAsync(memory);
     }
     memory.Position = 0;

     return File(memory, "application/octet-stream", fileName);
 }

 [HttpGet("stream/{videoName}")]
 public IActionResult StreamVideo(string videoName)
 {
     var videoPath = Path.Combine(_env.ContentRootPath, "Uploads", videoName);
     if (!System.IO.File.Exists(videoPath))
     {
         return NotFound();
     }

     var stream = new FileStream(videoPath, FileMode.Open, FileAccess.Read);
     var contentType = "video/mp4";

     // Check for Range request and support seeking
     var rangeHeader = Request.Headers[HeaderNames.Range];
     if (!string.IsNullOrEmpty(rangeHeader))
     {
         var range = Request.GetTypedHeaders().Range;
         if (range != null && range.Ranges.Count > 0)
         {
             var firstRange = range.Ranges.First();
             var from = firstRange.From ?? 0;
             var to = firstRange.To ?? stream.Length - 1;

             if (from < 0 || to >= stream.Length || from > to)
             {
                 Response.Headers[HeaderNames.ContentRange] = $"bytes */{stream.Length}";
                 return StatusCode(416); // Range Not Satisfiable
             }

             var contentLength = to - from + 1;
             Response.StatusCode = 206; // Partial Content
             Response.Headers[HeaderNames.ContentRange] = $"bytes {from}-{to}/{stream.Length}";
             Response.ContentLength = contentLength;

             return File(stream, contentType, enableRangeProcessing: true);
         }
     }

     // If no range header, serve the entire video
     return File(stream, contentType);
 }
}   