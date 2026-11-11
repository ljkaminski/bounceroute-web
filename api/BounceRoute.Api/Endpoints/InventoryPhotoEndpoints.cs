using BounceRoute.Api.Contracts;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BounceRoute.Api.Endpoints;

public static class InventoryPhotoEndpoints
{
	// The web app shrinks photos before uploading, so real uploads are a few hundred KB.
	public const int MaxPhotoBytes = 3 * 1024 * 1024;

	public static void MapInventoryPhotoEndpoints(this IEndpointRouteBuilder app)
	{
		var photos = app.MapGroup("/api/inventory/{id:int}/photo").WithTags("Inventory");

		// Crews see photos on their loading list, so any signed-in user can view them.
		photos.MapGet("/", GetPhoto).RequireAuthorization();

		photos.MapPut("/", UploadPhoto)
			.RequireAuthorization(Policies.Office)
			.Accepts<byte[]>("image/jpeg", "image/png", "image/webp")
			.WithSummary("Sets the item's photo. Send the image bytes as the request body (JPEG, PNG, or WebP, up to 3 MB).");

		photos.MapDelete("/", DeletePhoto).RequireAuthorization(Policies.Office);
	}

	private static async Task<Results<FileContentHttpResult, NotFound>> GetPhoto(int id, HttpContext context, BounceRouteDbContext db)
	{
		var photo = await db.InventoryPhotos.FindAsync(id);
		if (photo is null)
			return TypedResults.NotFound();

		// The URL carries a version number that changes on upload, so the browser can keep this forever.
		context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
		context.Response.Headers.XContentTypeOptions = "nosniff";
		return TypedResults.File(photo.Data, photo.ContentType);
	}

	private static async Task<Results<Ok<InventoryItemDto>, NotFound, ValidationProblem>> UploadPhoto(
		int id, HttpRequest request, BounceRouteDbContext db, TimeProvider time)
	{
		var item = await db.InventoryItems.FindAsync(id);
		if (item is null)
			return TypedResults.NotFound();

		var data = await ReadUpToAsync(request.Body, MaxPhotoBytes + 1);
		if (data.Length > MaxPhotoBytes)
			return Problem("Photos can be up to 3 MB.");

		// Trust the bytes, not the Content-Type header.
		if (DetectImageType(data) is not string contentType)
			return Problem("That isn't a JPEG, PNG, or WebP image.");

		var photo = await db.InventoryPhotos.FindAsync(id);
		if (photo is null)
		{
			photo = new InventoryPhoto { InventoryItemId = id };
			db.InventoryPhotos.Add(photo);
		}
		photo.ContentType = contentType;
		photo.Data = data;
		photo.UploadedAt = time.GetUtcNow();
		item.PhotoVersion = (item.PhotoVersion ?? 0) + 1;

		await db.SaveChangesAsync();
		return TypedResults.Ok(InventoryEndpoints.ToDto(item));
	}

	private static async Task<Results<Ok<InventoryItemDto>, NotFound>> DeletePhoto(int id, BounceRouteDbContext db)
	{
		var item = await db.InventoryItems.FindAsync(id);
		if (item is null)
			return TypedResults.NotFound();

		var photo = await db.InventoryPhotos.FindAsync(id);
		if (photo is not null)
			db.InventoryPhotos.Remove(photo);
		item.PhotoVersion = null;

		await db.SaveChangesAsync();
		return TypedResults.Ok(InventoryEndpoints.ToDto(item));
	}

	internal static string? DetectImageType(ReadOnlySpan<byte> data)
	{
		if (data.StartsWith((byte[])[0xFF, 0xD8, 0xFF]))
			return "image/jpeg";
		if (data.StartsWith((byte[])[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]))
			return "image/png";
		if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
			return "image/webp";
		return null;
	}

	// Stops reading once past the limit so an oversized upload isn't buffered in full.
	private static async Task<byte[]> ReadUpToAsync(Stream body, int limit)
	{
		using var buffer = new MemoryStream();
		var chunk = new byte[81920];
		int read;
		while (buffer.Length < limit && (read = await body.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, limit - buffer.Length)))) > 0)
			buffer.Write(chunk, 0, read);
		return buffer.ToArray();
	}

	private static ValidationProblem Problem(string message) =>
		TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Photo"] = [message] });
}
