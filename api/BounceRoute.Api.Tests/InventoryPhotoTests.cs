using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Tests;

public sealed class InventoryPhotoTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
	// Enough of a PNG for the type check: the 8-byte signature plus a few bytes.
	private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

	private HttpClient office = null!;

	public async Task InitializeAsync() => office = await factory.SignedInClientAsync(Roles.Office);

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Uploaded_photo_comes_back_with_its_type_and_a_cacheable_url()
	{
		var item = await CreateItemAsync();

		var updated = await (await UploadAsync(item.Id, Png, "image/png")).ReadAsync<InventoryItemDto>();
		Assert.Equal($"/api/inventory/{item.Id}/photo?v=1", updated.PhotoUrl);

		var photo = await office.GetAsync(updated.PhotoUrl);
		Assert.Equal("image/png", photo.Content.Headers.ContentType?.MediaType);
		Assert.Equal(Png, await photo.Content.ReadAsByteArrayAsync());
		Assert.Contains("immutable", photo.Headers.CacheControl?.ToString());
	}

	[Fact]
	public async Task Replacing_a_photo_changes_its_url()
	{
		var item = await CreateItemAsync();
		await UploadAsync(item.Id, Png, "image/png");

		var second = await (await UploadAsync(item.Id, [0xFF, 0xD8, 0xFF, 0xE0, 0, 0], "image/jpeg")).ReadAsync<InventoryItemDto>();

		Assert.EndsWith("?v=2", second.PhotoUrl);
		Assert.Equal("image/jpeg", (await office.GetAsync(second.PhotoUrl)).Content.Headers.ContentType?.MediaType);
	}

	[Fact]
	public async Task Files_that_are_not_images_are_rejected_whatever_they_claim_to_be()
	{
		var item = await CreateItemAsync();

		var response = await UploadAsync(item.Id, "<script>alert(1)</script>"u8.ToArray(), "image/png");

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task Photos_over_the_size_limit_are_rejected()
	{
		var item = await CreateItemAsync();
		var huge = new byte[3 * 1024 * 1024 + 10];
		Png.CopyTo(huge, 0);

		var response = await UploadAsync(item.Id, huge, "image/png");

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Contains("3 MB", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Removing_a_photo_clears_the_url()
	{
		var item = await CreateItemAsync();
		await UploadAsync(item.Id, Png, "image/png");

		var cleared = await (await office.DeleteAsync($"/api/inventory/{item.Id}/photo")).ReadAsync<InventoryItemDto>();

		Assert.Null(cleared.PhotoUrl);
		Assert.Equal(HttpStatusCode.NotFound, (await office.GetAsync($"/api/inventory/{item.Id}/photo")).StatusCode);
	}

	[Fact]
	public async Task Crew_can_view_photos_but_not_change_them()
	{
		var item = await CreateItemAsync();
		await UploadAsync(item.Id, Png, "image/png");
		var crew = await factory.SignedInClientAsync(Roles.Crew);

		Assert.Equal(HttpStatusCode.OK, (await crew.GetAsync($"/api/inventory/{item.Id}/photo")).StatusCode);
		var upload = new ByteArrayContent(Png);
		upload.Headers.ContentType = new MediaTypeHeaderValue("image/png");
		Assert.Equal(HttpStatusCode.Forbidden, (await crew.PutAsync($"/api/inventory/{item.Id}/photo", upload)).StatusCode);
	}

	[Fact]
	public async Task Signed_out_visitors_cannot_view_photos()
	{
		var item = await CreateItemAsync();
		await UploadAsync(item.Id, Png, "image/png");

		Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"/api/inventory/{item.Id}/photo")).StatusCode);
	}

	private async Task<InventoryItemDto> CreateItemAsync() =>
		await (await office.PostAsJsonAsync("/api/inventory", InventoryTests.Item($"Photo {Guid.NewGuid():N}"), ApiFactory.Json))
			.ReadAsync<InventoryItemDto>();

	private Task<HttpResponseMessage> UploadAsync(int itemId, byte[] data, string contentType)
	{
		var content = new ByteArrayContent(data);
		content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
		return office.PutAsync($"/api/inventory/{itemId}/photo", content);
	}
}
