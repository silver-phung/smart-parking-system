using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.Common.Exceptions;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.DTOs.Images;
using SmartParking.Domain.Constants;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/images")]
[Produces("application/json")]
[Authorize]
public sealed class ImagesController : ControllerBase
{
    private readonly IImageService _imageService;

    public ImagesController(IImageService imageService)
    {
        _imageService = imageService;
    }

    /// <summary>Uploads a JPEG, PNG, or WebP image to Cloudinary.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(StoredImageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "A non-empty image file is required in the 'file' field." });

        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { message = "Unable to identify the authenticated user." });

        try
        {
            await using var stream = file.OpenReadStream();
            var image = await _imageService.UploadAsync(
                stream, file.ContentType, file.Length, userId, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = image.Id }, new
            {
                message = "Image uploaded successfully.",
                data = image
            });
        }
        catch (ImageValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (ImageConfigurationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message });
        }
        catch (ImageProviderException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
        catch (ImagePersistenceException exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = exception.Message });
        }
    }

    /// <summary>Gets image metadata if it belongs to the caller or the caller is an administrator.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(StoredImageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { message = "Unable to identify the authenticated user." });

        var image = await _imageService.GetByIdAsync(
            id, userId, User.IsInRole(Roles.Admin), cancellationToken);
        return image is null
            ? NotFound(new { message = "Image was not found." })
            : Ok(new { message = "Image metadata retrieved successfully.", data = image });
    }

    /// <summary>Deletes an image owned by the caller or any image when called by an administrator.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { message = "Unable to identify the authenticated user." });

        try
        {
            var result = await _imageService.DeleteAsync(
                id, userId, User.IsInRole(Roles.Admin), cancellationToken);
            return result switch
            {
                ImageDeleteResult.Deleted => NoContent(),
                ImageDeleteResult.NotFound => NotFound(new { message = "Image was not found." }),
                ImageDeleteResult.Forbidden => Forbid(),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }
        catch (ImageConfigurationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message });
        }
        catch (ImageProviderException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
        catch (ImagePersistenceException exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = exception.Message });
        }
    }

    private bool TryGetCurrentUserId(out long userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(claim, out userId);
    }
}
