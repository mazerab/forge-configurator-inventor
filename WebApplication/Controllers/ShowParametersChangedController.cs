using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.Forge.Client;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Utilities;

namespace WebApplication.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ShowParametersChangedController : ControllerBase
    {
        private readonly IForgeOSS _forgeOSS;
        private readonly ResourceProvider _resourceProvider;

        public ShowParametersChangedController(IForgeOSS forgeOSS, ResourceProvider resourceProvider)
        {
            _forgeOSS = forgeOSS;
            _resourceProvider = resourceProvider;
        }

        [HttpGet]
        public async Task<bool> Get()
        {
            bool result = true;

            ApiResponse<dynamic> ossObjectResponse = null;

            try
            {
                ossObjectResponse = await _forgeOSS.GetObjectAsync(_resourceProvider.BucketKey, ONC.ShowParametersChanged);
            } 
            catch (ApiException ex) when (ex.ErrorCode == 404)
            {
                // the file is not found. Just swallow the exception
            }

            if(ossObjectResponse != null)
            {
                using (Stream objectStream = ossObjectResponse.Data)
                {
                    result = await JsonSerializer.DeserializeAsync<bool>(objectStream);
                }
            }

            return result;
        }

        [HttpPost]
        public async Task<bool> Set([FromBody]bool show)
        {
            await _forgeOSS.UploadObjectAsync(_resourceProvider.BucketKey, ONC.ShowParametersChanged, Json.ToStream(show));
            return show;
        }
    }
}