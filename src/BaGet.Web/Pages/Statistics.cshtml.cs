using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaGet.Web
{
    public class StatisticsModel : PageModel
    {
        private readonly StatisticsService _statistics;

        public StatisticsModel(StatisticsService statistics)
        {
            _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
        }

        public FeedStatistics Statistics { get; private set; }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            if (_statistics.IsSupported)
            {
                Statistics = await _statistics.GetAsync(cancellationToken);
            }
        }
    }
}
