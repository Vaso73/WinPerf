using System.Net;
using System.Net.Http.Headers;
using WinPerf.Core.Updates;

namespace WinPerf.Tests.Updates;

public sealed class GitHubAvatarServiceTests
{
    [Fact]
    public async Task DownloadAsync_FetchesGitHubAvatarFromProfile()
    {
        var avatarBytes = new byte[] { 1, 2, 3, 4 };
        using var service = new GitHubAvatarService(new HttpClient(new DelegateHandler(request =>
        {
            if (request.RequestUri?.AbsoluteUri == "https://api.github.com/users/Vaso73")
            {
                return JsonResponse("""{"avatar_url":"https://avatars.githubusercontent.com/u/123?v=4"}""");
            }

            Assert.Equal("https://avatars.githubusercontent.com/u/123?v=4", request.RequestUri?.AbsoluteUri);
            return ImageResponse(avatarBytes);
        })));

        var result = await service.DownloadAsync("Vaso73");

        Assert.Equal(avatarBytes, result);
    }

    [Theory]
    [InlineData("-bad")]
    [InlineData("bad-")]
    [InlineData("bad--name")]
    [InlineData("bad/name")]
    public async Task DownloadAsync_RejectsInvalidGithubLogins(string login)
    {
        using var service = new GitHubAvatarService(new HttpClient(new DelegateHandler(_ =>
            throw new InvalidOperationException("HTTP should not be called for invalid login."))));

        var result = await service.DownloadAsync(login);

        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadAsync_RejectsNonGithubAvatarHost()
    {
        using var service = new GitHubAvatarService(new HttpClient(new DelegateHandler(_ =>
            JsonResponse("""{"avatar_url":"https://example.com/avatar.png"}"""))));

        var result = await service.DownloadAsync("Vaso73");

        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadAsync_RejectsOversizedAvatar()
    {
        using var service = new GitHubAvatarService(new HttpClient(new DelegateHandler(request =>
        {
            if (request.RequestUri?.Host == "api.github.com")
            {
                return JsonResponse("""{"avatar_url":"https://avatars.githubusercontent.com/u/123?v=4"}""");
            }

            return ImageResponse(new byte[GitHubAvatarService.MaximumAvatarBytes + 1]);
        })));

        var result = await service.DownloadAsync("Vaso73");

        Assert.Null(result);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue("application/json")
                }
            }
        };
    }

    private static HttpResponseMessage ImageResponse(byte[] bytes)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue("image/png")
                }
            }
        };
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
