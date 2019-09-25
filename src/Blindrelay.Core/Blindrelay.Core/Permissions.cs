using Blindrelay.Core.Api;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Blindrelay.Core
{
    public partial class Client
    {
        public async Task<IEnumerable<UserGroupPermissions>> GetGroupPermissionsAsync()
        {
            var response = await apiService.UserGroupPermissionsGetAsync(new UserGroupPermissionsGetRequest
            {
            }, authToken.ToString());

            if (response.UserGroupPermissions == null)
                return new UserGroupPermissions[0];

            return response.UserGroupPermissions;
        }
    }
}
