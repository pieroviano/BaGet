using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Protocol.Models;
using Moq;
using Xunit;

namespace BaGet.Web.Tests;

public class IndexWorkflowFacts
{
    private readonly IndexWorkflow _target;
    private readonly ISearchService _searchService;

    private SearchRequest? _capturedRequest = null;
    private readonly SearchResponse _response = new SearchResponse();
    private readonly CancellationToken _cancellation = CancellationToken.None;

    public IndexWorkflowFacts()
    {
        var search = new Mock<ISearchService>();
        search
            .Setup(s => s.SearchAsync(It.IsAny<SearchRequest>(), _cancellation))
            .Callback((SearchRequest r, CancellationToken c) => _capturedRequest = r)
            .ReturnsAsync(_response);

        _searchService = search.Object;
        _target = new IndexWorkflow(_searchService, new StatisticsService(new NullContext()));
    }

    [Fact]
    public async Task DefaultSearch()
    {
        await _target.LoadAsync(new IndexViewModel(), null, _cancellation);

        #pragma warning disable CS8602
        Assert.Equal(0, _capturedRequest.Skip);
        #pragma warning restore CS8602
        Assert.Equal(20, _capturedRequest.Take);
        Assert.True(_capturedRequest.IncludePrerelease);
        Assert.True(_capturedRequest.IncludeSemVer2);
        Assert.Null(_capturedRequest.PackageType);
        Assert.Null(_capturedRequest.Framework);
        Assert.Null(_capturedRequest.Query);
    }

    [Fact]
    public async Task AcceptsParameters()
    {
        var model = new IndexViewModel
        {
            Prerelease = false,
            PageIndex = 5,
            PackageType = "foo",
            Framework = "bar",
            Query = "Hello world",
        };

        await _target.LoadAsync(model, null, _cancellation);

        #pragma warning disable CS8602
        Assert.Equal(80, _capturedRequest.Skip);
        #pragma warning restore CS8602
        Assert.Equal(20, _capturedRequest.Take);
        Assert.False(_capturedRequest.IncludePrerelease);
        Assert.True(_capturedRequest.IncludeSemVer2);
        Assert.Equal("foo", _capturedRequest.PackageType);
        Assert.Equal("bar", _capturedRequest.Framework);
        Assert.Equal("Hello world", _capturedRequest.Query);
    }

    [Fact]
    #pragma warning disable CS0618
    public async Task TheObsoletePageModelShimStillWorks()
    {
        var shim = new IndexModel(_searchService, new StatisticsService(new NullContext())) { PageIndex = 3, Query = "x" };

        await shim.OnGetAsync(_cancellation);

        #pragma warning disable CS8602
        Assert.Equal(40, _capturedRequest.Skip);
        #pragma warning restore CS8602
        Assert.Equal("x", _capturedRequest.Query);
        Assert.False(shim.IsHome);
    }
    #pragma warning restore CS0618
}
