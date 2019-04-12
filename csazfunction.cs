public static class ApplyPnPTemplateHTTPTrigger
{
    private static HttpClient httpClient = new HttpClient();
    
    [FunctionName("ApplyPnPTemplateHTTPTrigger")]
    public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequestMessage req, TraceWriter log, ExecutionContext executionContext)
    {
        log.Info("C# HTTP trigger function processed a request.");

        // Get request body
        dynamic data = await req.Content.ReadAsAsync<object>();

        // Get values from body data
        string webUrl = data?.webUrl;
        string callbackUrl = data?.callbackUrl;

        var authManager = new OfficeDevPnP.Core.AuthenticationManager();

        //Hardcoding ClientId and ClientSecret for snippet. 
        var clientContext = authManager.GetAppOnlyAuthenticatedContext(webUrl, "<your_client_id>", "<your_client_secret>");

        //Get Schema
        string currentDirectory = executionContext.FunctionDirectory;
        DirectoryInfo dInfo = new DirectoryInfo(currentDirectory);
        log.Info("Current directory:" + currentDirectory);
        var schemaDir = dInfo.Parent.FullName + "\\PnPSiteSchemas";
        log.Info("schemaDir:" + schemaDir);
        XMLTemplateProvider sitesProvider = new XMLFileSystemTemplateProvider(schemaDir, "");
        ProvisioningTemplate template = sitesProvider.GetTemplate("SiteCollectionSchema.xml");

        Web web = clientContext.Web;
        var author = web.Author;
        clientContext.Load(web);
        clientContext.Load(author);
        clientContext.ExecuteQueryRetry();

        //Apply template.
        log.Info($"Applying Provisioning template to site: {clientContext.Web.Url}");
        ProvisioningTemplateApplyingInformation ptai = new ProvisioningTemplateApplyingInformation
        {
            ProgressDelegate = (message, progress, total) =>
            {
                log.Info(string.Format("{0:00}/{1:00} - {2}", progress, total, message));
            }
        };
        web.ApplyProvisioningTemplate(template, ptai);

        //call back Url 
        log.Info($"Posting to callback url: {callbackUrl}");
        await httpClient.PostAsJsonAsync<string>(callbackUrl, $"{{'SiteOwnerEmail' : '{author.Email}'}}");

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
