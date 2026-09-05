using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using Microsoft.AspNetCore.Http;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize]
    public class FileStorageController(IFileStorageService fileStorageService) : ControllerBase
    {
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string folder = "", CancellationToken cancellationToken = default)
        {
            if (file == null)
                return BadRequest();

            await using var stream = file.OpenReadStream();
            var result = await fileStorageService.UploadAsync(stream, file.FileName, file.ContentType, folder, cancellationToken);
            return StatusCode(200, result);
        }

        [HttpDelete]
        public async Task<IActionResult> Delete([FromQuery] string storedFileName, [FromQuery] string folder = "", CancellationToken cancellationToken = default)
        {
            await fileStorageService.DeleteAsync(storedFileName, folder, cancellationToken);
            return NoContent();
        }
    }
}
