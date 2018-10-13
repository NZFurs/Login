using System.Collections.Generic;

namespace NZFurs.Auth.Models.GrantsViewModels
{
    public class GrantsViewModel
    {
        public IEnumerable<GrantViewModel> Grants { get; set; }
    }
}