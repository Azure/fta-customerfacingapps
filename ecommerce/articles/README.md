﻿# POC Scenario: E-commerce Website

> **IMPORTANT:** The walkthrough below is based on .NET Core 2.0 which is no longer supported. The code in the main branch has been updated for newer versions and certain functional changes have been made as well which may make the steps below no longer match the code in the main branch. To follow along with the exact code that was used to document the walkthrough below, please refer to the [.NET Core 2.0 branch](https://github.com/Azure/fta-customerfacingapps/tree/netcore2.0).

## Table Of Contents

* [Introduction](#introduction)
  * [Abstract](#abstract)
  * [Learning objectives](#learning-objectives)
* [Preparation](#preparation)
  * [Prerequisites](#prerequisites)
  * [Plan your deployment](#plan-your-deployment)
  * [Open the application with Visual Studio](#open-the-application-with-visual-studio)
* [Deployment Steps - Core Services](#deployment-steps---core-services)
  * [Create an Azure Resource Group](#create-an-azure-resource-group)
  * [Deploy the Web App](#deploy-the-web-app)
  * [Create the SQL Database containing the concerts](#create-the-sql-database-containing-the-concerts)
  * [Enable user sign-in via Azure AD B2C](#enable-user-sign-in-via-azure-ad-b2c)
  * [Set up Azure Search connected to the SQL Database](#set-up-azure-search-connected-to-the-sql-database)
  * [Enable background ticket image generation via a Function App](#enable-background-ticket-image-generation-via-a-function-app)
* [Deployment Steps - Optional Services](#deployment-steps---optional-services)
  * [Add caching of upcoming concerts using Redis Cache](#add-caching-of-upcoming-concerts-using-redis-cache)
  * [Set up monitoring and analytics using Application Insights](#set-up-monitoring-and-analytics-using-application-insights)
  * [Use a Content Delivery Network for static files](#use-a-content-delivery-network-for-static-files)
  * [Add sentiment analysis for reviews using Cognitive Services](#add-sentiment-analysis-for-reviews-using-cognitive-services)
  * [Prepare the web app for global availability using Traffic Manager](#prepare-the-web-app-for-global-availability-using-traffic-manager)
  * [Set up a custom DNS domain](#set-up-a-custom-dns-domain)
  * [Configure an SSL certificate for your domain](#configure-an-ssl-certificate-for-your-domain)
* [Next Steps](#next-steps)

## Introduction

#### Abstract
Azure Platform-as-a-Service (PaaS) enables you to deploy enterprise grade e-commerce applications, and lets you adapt to the size and seasonality of your business. When demand for your products or services takes off — predictably or unpredictably — you can be prepared to handle more customers and more transactions automatically. Additionally, take advantage of cloud economics by paying only for the capacity you use. In short, focus on your sales and leave the infrastructure management to your cloud provider.

During this guided Proof-Of-Concept (POC) scenario, you will learn about bringing together various Azure PaaS components to deploy a sample e-commerce application, _Relecloud Concerts_, an online concert ticketing platform.

![Scenario Overview](media/scenario-overview.png)

#### Learning objectives
* Understanding the Azure App Service platform and building Web Apps with SQL Database
* Implementing search, user sign-up, background task processing, and caching
* Gaining insights into application and user behavior with Application Insights

## Preparation

#### Prerequisites
To complete this scenario, you will need:
* Visual Studio 2017 Update 3 or later with the "Azure development" features
* [ASP.NET Core 2.0](https://www.microsoft.com/net/core)
* An Azure subscription

#### Plan your deployment
* As part of this scenario, you will be deploying the following resources into a Resource Group:
  * App Service
  * Function App
  * Azure Active Directory B2C
  * Application Insights
  * Redis Cache
  * Search Service
  * SQL Database
  * Storage Account
  * Content Delivery Network
  * Cognitive Services
  * Traffic Manager
  * App Service Domain
  * DNS Zone
  * App Service Certificate
  * Key Vault
* When choosing names for your resources, try to follow a **standard naming pattern**, e.g. by following the [naming conventions documented on the Azure Architecture Center](https://docs.microsoft.com/en-us/azure/architecture/best-practices/naming-conventions)
  * To make it easier, we'll provide suggestions based on a naming prefix of your choosing, referred to as `<prefix>` from this point onwards
  * To ensure that your deployment does not conflict with other Azure customers, use a prefix that is unique to your organization
  * To ensure that your chosen prefix can be used for all resource types, use a relatively short name consisting only of lowercase letters (no digits, no other special characters) e.g. `contoso`
* Choose an **Azure region** to host your deployment
  * You can choose any supported region, but for performance and cost reasons it's recommended to keep all resources in the same region whenever possible
  * You can see the availability of all [Azure services by region](https://azure.microsoft.com/en-us/regions/services/) to determine the best location
  * For this scenario, choose a region where **Application Insights** (under the **DevOps** category in the link above) is available, as it is not currently present in all regions
* Whenever there could be multiple instances of the same type of resource in multiple regions (e.g. for a globally distributed web application that has an App Service in multiple regions), consider including an abbreviation of the region in the resource name
  * E.g. `eu` for East US or `we` for West Europe
  * We will refer to this as `<region>` in the suggested names below
* At the end your Resource Group may look something like this:

![Resource Group Overview](media/resourcegroup-overview.png)

#### Open the application with Visual Studio
* Clone the repository or copy the project's [source code](../src) to a local working folder
* From the working folder, open **Relecloud.sln** with Visual Studio
* ![Visual Studio Solution](media/visualstudio-solution.png)
* Explore the solution
  * The `Relecloud.Web` project contains the main e-commerce web application
    * This will be deployed as an Azure Web App
    * Especially the `Startup.cs` file is important as it contains the logic to hook up the necessary services based on the configuration
    * If a configuration setting is missing for a certain Azure service, it  will be replaced by a dummy implementation so you can build up the solution gradually without running into errors
  * The `Relecloud.FunctionApp` project contains functions to perform background event processing
    * This will be deployed as an Azure Function App

## Deployment Steps - Core Services

#### Create a Resource Group
> This allows you to group all the Azure resources mentioned above in a single container for easier management

* Navigate to the [Azure Portal](https://portal.azure.com) and sign in with your account
* [Create a Resource Group](https://docs.microsoft.com/en-us/azure/azure-resource-manager/resource-group-portal)
  * _Suggested name for the Resource Group: `<prefix>-prod-rg`_
  * ![Create Resource Group](media/resourcegroup-create.png)

#### Deploy the Web App
> This allows users to visit your e-commerce web application

* Build the `Relecloud.Web` project (e.g. in Visual Studio or on the command line) and ensure there are no errors
* Use any of the supported ways to [deploy the project to Azure App Service](https://docs.microsoft.com/en-us/azure/app-service-web/web-sites-deploy)
  * Note that the easiest way if you are using Visual Studio is to [publish the project directly to Azure](https://docs.microsoft.com/en-us/azure/app-service-web/app-service-web-get-started-dotnet#publish-to-azure)
* When creating the App Service:
  * _Suggested name for the App Service: `<prefix>-web-app-<region>`_
  * _Suggested name for the App Service Plan: `<prefix>-web-plan-<region>`_
  * Ensure to create the App Service Plan in the Resource Group you created before
  * Ensure to create the App Service Plan in the same Azure region as the Resource Group
  * Choose `S1 Standard` or higher as the pricing tier for the App Service Plan (lower pricing tiers do not support Traffic Manager which we can use later on)
  * ![Deploy Web App](media/webapp-deploy.png)
* After the deployment is complete, browse to the site at `http://<your-site-name>.azurewebsites.net`
  * You should be able to navigate around the site but there will not be any concerts and signing in will not work

![Web App Home Page](media/webapp-page-home.png)

#### Create the SQL Database containing the concerts
> This allows the stateless web application to store persistent data in a managed database service

* Back in the Azure Portal, navigate to the Resource Group and [create a SQL Database](https://docs.microsoft.com/en-us/azure/sql-database/sql-database-get-started-portal)
  * _Suggested name for the SQL Database: `<prefix>-sql-database`_
  * Choose `Basic` or higher as the pricing tier for the SQL Database
  * ![Create SQL Database](media/sqldatabase-create.png)
  * _Suggested name for the SQL Server: `<prefix>-sql-server`_
  * Ensure to create the firewall rule that allows Azure services to access the server
  * ![Create SQL Server](media/sqlserver-create.png)
* After the SQL Database has been created, navigate to its **Connection strings** blade in the Azure Portal and copy the **ADO.NET connection string** to the clipboard
* Navigate to the App Service for the Web App and open the **Application settings** blade
  * Under **Application settings** (note: _do not_ use **Connection strings**), add a new configuration setting:

Name | Value
---- | -----
App:SqlDatabase:ConnectionString | The connection string you copied before (with the user name and password placeholders replaced with their actual values)

![Configure Web App](media/webapp-settings-sqldatabase.png)

* Browse to the site again and click **Upcoming**, it should now be showing a few upcoming concerts
  * This is because when you configured the connection string in the web app, it caused a restart of the application - at which point it automatically initializes the database with the right schema and a few sample concerts

![Web App Upcoming Concerts](media/webapp-page-upcomingconcerts.png)

#### Enable user sign-in via Azure AD B2C
> This allows new users to sign up for your e-commerce application and to manage their profiles

* Go back to your Resource Group in the Azure Portal and [create a new Azure Active Directory B2C tenant](https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-get-started)
  * Ensure to create a _new_ tenant, not to link an existing one
  * ![Create Azure AD B2C](media/aadb2c-create.png)
  * After the Azure AD B2C tenant is created, copy the full domain name from the **Overview** blade (e.g. `relecloudconcerts.onmicrosoft.com`) and paste it in Notepad for later
  * Please note, remember to do a full browser refresh to ensure you can see the new Azure AD tenant you created when you click on your profile in the top right hand corner
* [Register the web application in the Azure AD B2C tenant](https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-app-registration)
  * Follow the steps to register a **web app**
  * For the **Name** (which will be seen by users when they sign in), use `Relecloud Concerts`
  * For the **Reply URL**, use `https://<your-site-name>.azurewebsites.net/signin-oidc`
    * Note that this URL must be secure (i.e. it must use **https**)
    * Fortunately Azure App Service provides a valid SSL certificate for the `*.azurewebsites.net` domain out of the box
  * ![Register Web App in Azure AD B2C](media/aadb2c-register-webapp.png)
* After the application is registered, open its **Properties** blade
  * Copy the **Application ID** to Notepad
* Go back to the Azure AD B2C **Applications** blade to create the necessary policies
* Create a combined [sign-up or sign-in policy](https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-reference-policies#create-a-sign-up-or-sign-in-policy)
  * For the policy **Name**, use `SignUpOrIn`
  * For the **Sign-up attributes**, select at least `Display Name` and `Email Address`
  * For the **Application claims**, select at least `Display Name`, `Email Addresses` and `User's Object ID`
  * ![Add Sign Up Or In Sign Policy to Azure AD B2C](media/aadb2c-policy-signuporin.png)
  * After the policy is created, copy its full name to Notepad (including the `B2C_1_` prefix that is automatically appended)
* Create a [profile editing policy](https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-reference-policies#create-a-profile-editing-policy)
  * For the policy **Name**, use `EditProfile`
  * For the **Profile attributes**, select at least `Display Name` so the user can edit their name as it is displayed in the web application
  * For the **Application claims**, select at least `Display Name`, `Email Addresses` and `User's Object ID`
  * After the policy is created, copy its full name to Notepad (including the `B2C_1_` prefix that is automatically appended)
* Create a [password reset policy](https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-reference-policies#create-a-password-reset-policy)
  * For the policy **Name**, use `ResetPassword`
  * For the **Application claims**, select at least `Display Name`, `Email Addresses` and `User's Object ID`
  * After the policy is created, copy its full name to Notepad (including the `B2C_1_` prefix that is automatically appended)
* Navigate to the App Service for the Web App and open the **Application settings** blade
  * Under **Application settings**, add the following new configuration settings from the values in Notepad:

Name | Value
---- | -----
App:Authentication:Tenant | The full domain name of your Azure AD B2C tenant (including `.onmicrosoft.com`)
App:Authentication:ClientId | The Client ID of the web app you registered
App:Authentication:SignUpSignInPolicyId | The name of the sign-up or sign-in policy, e.g. `B2C_1_SignUpOrIn`
App:Authentication:EditProfilePolicyId | The name of the profile editing policy, e.g. `B2C_1_EditProfile`
App:Authentication:ResetPasswordPolicyId | The name of the password reset policy, e.g. `B2C_1_ResetPassword`

* Browse to the site again, it should now allow you to register as a new user
  * Go through the sign in experience to register a new account using any email address (e.g. use a personal email to simulate an end user for your e-commerce application)
  * Notice that once you are signed in, the application will display your name and clicking it will allow you to change your profile stored in Azure AD B2C (i.e. the profile editing policy)
  * When signing in again later, also notice that there is a possibility to reset your password in case you forgot it (i.e. the password reset policy)
  * Important to note is that **no user credentials are ever stored or managed in the application database**: the complete identity management experience is handled and secured by Azure AD B2C
  * Also note that it is possible to completely [customize the user experience presented by Azure AD B2C](https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-ui-customization-custom) (e.g. to use custom branding images or a completely new look to match the style of your web application)
  * Finally, you can also configure Azure AD B2C to allow users to sign in with existing social identities such as their [Microsoft Account](https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-setup-msa-app), [Facebook](https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-setup-fb-app), [Google](https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-setup-goog-app), etc

#### Set up Azure Search connected to the SQL Database
> This allows your users to perform powerful searches for the concerts in the database with hit highlighting

* Go back to your Resource Group in the Azure Portal and [create a new Azure Search Service](https://docs.microsoft.com/en-us/azure/search/search-create-service-portal)
  * _Suggested name for the service name: `<prefix>-search`_
  * Choose `Basic` or higher as the pricing tier
  * ![Create Azure Search](media/search-create.png)
  * After the Azure Search service has been created, navigate to the **Keys** blade and copy the **Primary admin key** to the clipboard
* Navigate to the App Service for the Web App and open the **Application settings** blade
  * Under **Application settings**, add the following new configuration settings:

Name | Value
---- | -----
App:AzureSearch:ServiceName | The name of the Azure Search service (not the full DNS name)
App:AzureSearch:AdminKey | The primary admin key you copied before

* Browse to the site again; within a few minutes it should now allow you to search for concerts and highlight the matching text in the search results

![Web App Search](media/webapp-page-search.png)

* Note that the configuration of the connection (the "[indexer](https://docs.microsoft.com/en-us/azure/search/search-howto-connecting-azure-sql-database-to-azure-search-using-indexers)") between the SQL Database (the "data source") and the Azure Search service (the "[index](https://docs.microsoft.com/en-us/azure/search/search-what-is-an-index)") is performed automatically by the application upon startup when configured with the settings you just added
* Browse through the blade for the Azure Search service you just created in the portal
  * Open the **index** to see which fields are maintained in the search index
  * Open the **data source** to see the connection to SQL Database that is used to keep the index up to date with any changes to the concert data
  * Open the **indexers** to see when the data was last indexed

#### Enable background ticket image generation via a Function App
> This allows users to get a printable ticket with a barcode when they purchase tickets

* Go back to your Resource Group in the Azure Portal and [create a new Storage Account](https://docs.microsoft.com/en-us/azure/storage/common/storage-create-storage-account#create-a-storage-account)
  * _Suggested name for the Storage Account: `<prefix>storage`_ (note that special characters are not allowed here)
  * The Queues service will be used to send an event to a background application whenever a user purchases a ticket
  * The Blobs service will be used to securely store the generated ticket images
  * ![Create Storage Account](media/storage-create.png)
  * After the Storage Account has been created, navigate to the **Access keys** blade and copy the **connection string** for **key1** to the clipboard
* Navigate to the App Service for the Web App and open the **Application settings** blade
  * Under **Application settings**, add the following new configuration settings:

Name | Value
---- | -----
App:StorageAccount:ConnectionString | The connection string you copied before
App:StorageAccount:EventQueueName | A name for the queue through which event messages will be sent, e.g. `events`

* Build the `Relecloud.FunctionApp` project (e.g. in Visual Studio or on the command line) and ensure there are no errors
* Use any of the supported ways to [deploy the project to an Azure Function App](https://docs.microsoft.com/en-us/azure/azure-functions/functions-infrastructure-as-code)
  * Note that the easiest way if you are using Visual Studio 2017 or above is to [publish the project directly to Azure](https://docs.microsoft.com/en-us/azure/azure-functions/functions-develop-vs#publish-to-azure)
  * If the **Azure Function App** option is missing from the publishing targets you might need to install an update for the **Azure Functions and Web Job Tools** Visual Studio extension. You should see a notification flag in the upper right corner which will allow you to update the extension.
* When creating the Function App:
  * _Suggested name for the App Service: `<prefix>-func-app`_
  * _Suggested name for the App Service Plan: `<prefix>-func-plan`_
  * Ensure to create the App Service Plan in the Resource Group you created before
  * Ensure to create the App Service Plan in the same Azure region as the Resource Group
  * Choose `Consumption` as the pricing tier for the App Service Plan so you only pay when the function is effectively running (use another pricing tier if this is not supported in your chosen Azure region)
  * Choose the Storage Account you created before to store the required function app state
  * ![Deploy Function App](media/functionapp-deploy.png)
* Navigate to the Function App in the Azure Portal and open the **Application settings** tab
  * Under **Application settings**, add the following new configuration settings:

Name | Value
---- | -----
App:StorageAccount:ConnectionString | The same as in the Web App
App:StorageAccount:EventQueueName | The same as in the Web App
App:SqlDatabase:ConnectionString | The same as in the Web App

![Function App Settings](media/functionapp-settings-storage-sqldatabase.png)

* Browse to the site again and purchase some tickets
  * At first, the tickets page should be showing a message that the ticket is still being generated
  * The ticket image will be generated in the background by the Function App (triggered by a message in a queue), stored into blob storage, at which point the ticket in the database will be updated with a URL to the image (secured by a [Shared Access Signature](https://docs.microsoft.com/en-us/azure/storage/common/storage-dotnet-shared-access-signature-part-1))
  * After a little while, a ticket image should appear when you refresh the tickets page

![Web App Tickets](media/webapp-page-tickets.png)

## Deployment Steps - Optional Services

#### Add caching of upcoming concerts using Redis Cache
> This increases the performance of the most visited page of the web application by keeping the list of upcoming concerts in a memory cache for fast retrieval

* Go back to your Resource Group in the Azure Portal and [create a new Redis Cache](https://docs.microsoft.com/en-us/azure/redis-cache/cache-dotnet-how-to-use-azure-redis-cache#create-cache)
  * _Suggested name for the DNS name: `<prefix>-redis`_
  * Choose `Basic C1` or higher as the pricing tier (`C0` is not preferred since it uses shared CPU cores)
  * ![Create Redis Cache](media/redis-create.png)
  * After the Redis Cache has been created, navigate to the **Access keys** blade and copy the **Primary connection string (StackExchange.Redis)** to the clipboard
* Navigate to the App Service for the Web App and open the **Application settings** blade
  * Under **Application settings** (note: _do not_ use **Connection strings**), add a new configuration setting:

Name | Value
---- | -----
App:RedisCache:ConnectionString | The connection string you copied before

* Browse to the site again, it should now be showing the same upcoming concerts page (with improved performance if at all noticeable)

#### Set up monitoring and analytics using Application Insights
> This allows you to get insights on user and application behavior

* Go back to your Resource Group in the Azure Portal and [create a new Application Insights resource](https://docs.microsoft.com/en-us/azure/application-insights/app-insights-create-new-resource)
  * _Suggested name for the Application Insights resource: `<prefix>-appinsights`_
  * ![Create Application Insights](media/appinsights-create.png)
  * After the Application Insights resource has been created, navigate to the **Properties** blade and copy the **Instrumentation Key** to the clipboard
* Navigate to the App Service for the Web App and open the **Application settings** blade
  * Under **Application settings**, add the following new configuration setting:

Name | Value
---- | -----
ApplicationInsights:InstrumentationKey | The instrumentation key you copied before

* Browse to the site again and visit a few pages
  * This should start flowing data into Application Insights in the background
* Go to the **Overview** blade of your Application Insights resource and open the **Search** blade
  * This allows you to see incoming data in near real time
* [Learn more about Application Insights](https://docs.microsoft.com/en-us/azure/application-insights/app-insights-overview) to understand what else you can do with this very powerful service

#### Use a Content Delivery Network for static files
> This speeds up the performance of your e-commerce site for your end users

* Go back to your Resource Group in the Azure Portal and [create a new CDN Profile](https://docs.microsoft.com/en-us/azure/cdn/cdn-create-new-endpoint)
  * _Suggested name for the CDN Profile: `<prefix>-cdn`_
  * _Suggested name for the CDN Endpoint: `<prefix>`_
  * For this scenario, consider using the `Standard Akamai` pricing tier, as Akamai has a shorter propagation time than Verizon (so you will see the content being served typically within the minute already)
  * When creating the CDN Profile, also create an Endpoint for the Web App immediately
  * ![Create CDN Profile](media/cdnprofile-create.png)
  * After the CDN Endpoint has been created, navigate to its **Overview** blade and copy the **Endpoint hostname** (e.g. `https://<prefix>.azureedge.net`) to the clipboard
* Navigate to the App Service for the Web App and open the **Application settings** blade
  * Under **Application settings**, add the following new configuration setting:

Name | Value
---- | -----
App:Cdn:Url | The CDN Endpoint URL you copied before

* Browse to the site again and look at the source for the home page
  * The image as well as the site-specific CSS and JavaScript files should now be served from the CDN

#### Add sentiment analysis for reviews using Cognitive Services
> This allows you to determine whether the sentiment of a user review is positive or negative

* Go back to your Resource Group in the Azure Portal and [create a new Text Analytics API](https://docs.microsoft.com/en-us/azure/cognitive-services/cognitive-services-apis-create-account)
  * _Suggested name for the Text Analytics API resource: `<prefix>-cognitiveservices`_
  * For the **Pricing tier**, choose the `Free F0` if possible or the `Standard S` otherwise
  * ![Create Text Analytics API](media/cognitiveservices-create.png)
* After the Text Analytics API has been created
  * Open the **Overview** blade and copy the **Endpoint** URL to Notepad
  * Open the **Keys** blade and copy **Key 1** to Notepad
* Navigate to the Function App in the Azure Portal and open the **Application settings** tab
  * Under **Application settings**, add the following new configuration settings:

Name | Value
---- | -----
App:CognitiveServices:EndpointUri | The Endpoint URL you copied before
App:CognitiveServices:ApiKey | The key you copied before

* Browse to the site again and add some reviews
  * The sentiment of the review will be analyzed in the background by the Function App (triggered by a message in a queue), at which point the review in the database will be updated with a score
  * After a little while, a sentiment score should appear for a review when you refresh the concert details page

![Web App Concert Review](media/webapp-page-review.png)

#### Prepare the web app for global availability using Traffic Manager
> This allows you to prepare for global availability of your e-commerce site so that users always connect to the web app instance that is closest to their location

* Go back to your Resource Group in the Azure Portal and [create a new Traffic Manager profile](https://docs.microsoft.com/en-us/azure/traffic-manager/traffic-manager-create-profile)
  * _Suggested name for the Traffic Manager profile: `<prefix>-traffic`_
  * For the **Routing method**, choose `Performance`
  * ![Create Traffic Manager Profile](media/trafficmanager-create.png)
  * After the Traffic Manager profile has been created, open its **Endpoints** blade and [add an endpoint](https://docs.microsoft.com/en-us/azure/traffic-manager/traffic-manager-manage-endpoints#to-add-a-cloud-service-or-an-app-service-endpoint-to-a-traffic-manager-profile) pointing at the Web App
  * For the **Name**, choose the same name as the Web App, e.g. `<prefix>-web-app-<region>`
  * For the **Target resource type**, choose **App Service** and select the Web App
  * If you would add new Web App instances in other Azure regions later on, you can add them as endpoints here and they would get added to the Traffic Manager DNS name without any end user impact
  * ![Add Traffic Manager Endpoint](media/trafficmanager-endpoint-add.png)
* Open the **Configuration** blade for the Traffic Manager profile
  * Notice that the **Routing method** is set to **Performance**, which means that the DNS name will resolve to the instance of the Web App that has the best performance for the user's location (usually the geographically closest region)
  * Also notice that by default, Traffic Manager will probe the back-end resource for health using `HTTP` on port `80`, but the web application is configured to require `https` so it will send an HTTP redirect status code causing the probe to fail
  * To fix this, change the **Endpoint monitor settings** to use `HTTPS` on port `443`
  * ![Update Traffic Manager Configuration](media/trafficmanager-configuration.png)
* Now browse to the `http://<prefix>.trafficmanager.net` site
  * As explained above, notice that the web application will redirect the browser to `https://<prefix>.trafficmanager.net`
  * At this point, the web application is still using an SSL certificate for `*.azurewebsites.net` so the browser will warn you about an invalid SSL certificate (since the host name does not match), which in this particular case can be safely ignored but will be fixed in a later step
* When attempting to sign in to the web application via this Traffic Manager URL, you will get an error because the URL is not configured in Azure AD B2C
  * Navigate back to the Azure AD B2C tenant and add the `https://<prefix>.trafficmanager.net/signin-oidc` Reply URL to the registered Web Application
  * Notice that you must remove the old Reply URL as it's currently not allowed to configure more than one external domain
  * After the configuration is updated, you should now be able to sign in again with the same account as before

#### Set up a custom DNS domain
> This moves your e-commerce site to its own internet domain to increase visibility and trust

* If you already own a domain that you want to use for your Web App, follow the steps to [map an existing custom DNS name](https://docs.microsoft.com/en-us/azure/app-service/app-service-web-tutorial-custom-domain) and skip the next step
* Back in the Azure Portal, navigate to the App Service for the Web App, open the **Custom domains** blade and [buy a new App Service Domain](https://docs.microsoft.com/en-us/azure/app-service/custom-dns-web-site-buydomains-web-app#buy-the-domain)
  * When assigning default hostnames, select only `www`  and not `@ (Root Domain)` as this isn't supported by Traffic Manager
  * ![Create App Service Domain](media/appservicedomain-create.png)
* At this time, the DNS record for the `www` subdomain points directly to the Web App's DNS name (e.g. `http://<your-site-name>.azurewebsites.net`), which needs to be changed to the Traffic Manager endpoint by following the steps for [configuring a custom domain name for a web app in Azure App Service using Traffic Manager](https://docs.microsoft.com/en-us/azure/app-service/web-sites-traffic-manager-custom-domain-name)
  * If you purchased the domain through the App Service Domain service as explained above, you can manage your DNS records in Azure directly
  * Back in the Azure Portal, navigate to the DNS zone resource for your custom domain and update the `www` CNAME record to point to your Traffic Manager endpoint (e.g. `<prefix>.trafficmanager.net`)
  * ![Update DNS Zone](media/dnszone-update.png)
* At this time, the CDN Endpoint also still points at the original Web App URL which must be updated
  * Navigate to the CDN Endpoint resource and open the **Origin** blade
  * Update the **Origin hostname** and **Origin host header** to match your custom domain (including `www`)
  * ![Update CDN Endpoint](media/cdnendpoint-update.png)
* When the custom domain has been configured successfully, browse to the website to verify that it works
  * Notice that like before with Traffic Manager, the web application is configured to require `https` so it will redirect the browser and show a warning about an invalid SSL certificate - but this will be fixed in a later step
* Also like before, when attempting to sign in to the web application via your custom domain, you will get an error because the URL is not configured in Azure AD B2C
  * Navigate back to the Azure AD B2C tenant and change the Reply URL for the registered Web Application from the Traffic Manager endpoint to `https://<your-domain>/signin-oidc` (e.g. `https://www.relecloudconcerts.com/signin-oidc`)
  * After the configuration is updated, you should now be able to sign in again with the same account as before

### Configure an SSL certificate for your domain
> This ensures your users have a safe online experience when using your e-commerce site

* If you already own an SSL certificate that you want to use for your Web App, follow the steps to [bind an existing custom SSL certificate](https://docs.microsoft.com/en-us/Azure/app-service/app-service-web-tutorial-custom-ssl) and skip this section
* Go back to your Resource Group in the Azure Portal and [create a new App Service Certificate](https://docs.microsoft.com/en-us/azure/app-service/web-sites-purchase-ssl-web-site)
  * ![Create App Service Certificate](media/appservicecertificate-create.png)
* Once the certificate is ready (and you may have to check your email to complete a verification step), navigate to the App Service Certificate resource in the portal to place it in Key Vault
  * If you don't have a Key Vault yet, you are prompted to create one
  * _Suggested name for the Key Vault: `<prefix>-vault`_
  * ![Create Key Vault](media/keyvault-create.png)
* Follow the other steps in the documentation referred to above to verify the domain ownership (if needed), assign the certificate to the Web App and add the SSL binding
* At this point, you should be able to navigate to the web application on your custom domain using `https` without any browser warnings

## Next Steps

This concludes the scenario. Please find some additional things you can do at this point below.

#### Deploy using an ARM Template
As an alternative to manually setting up all the necessary resources in Azure and connecting the various services together (e.g. configuring App Settings to connect to other resources, enlisting the main Web App into Traffic Manager, ...), you can use an Azure Resource Manager (ARM) template.
* Learn more about [ARM templates](https://docs.microsoft.com/en-us/azure/azure-resource-manager/resource-manager-create-first-template)
* The ARM template for this solution is included in the [Relecloud.Arm](../src/Relecloud.Arm) folder
* You can publish it in various ways, e.g.: 
  * Using [PowerShell](https://docs.microsoft.com/en-us/azure/azure-resource-manager/resource-group-template-deploy)
  * Using [Visual Studio](https://docs.microsoft.com/en-us/azure/azure-resource-manager/vs-azure-tools-resource-groups-deployment-projects-create-deploy#deploy-the-resource-group-project-to-azure)
  * Using [Visual Studio Code](https://docs.microsoft.com/en-us/azure/azure-resource-manager/resource-manager-vscode-extension)
  * ...
* After the various resources have been created, you only have to publish the `Relecloud.Web` project to the Azure Web App, and the `Relecloud.FunctionApp` project to the Azure Function App as explained above (the app settings for both are automatically populated)

**Important note:** the ARM template creates and configures everything up to and including Traffic Manager, but not the custom domain (since that cannot currently be done via ARM) or the SSL certificate (since that depends on the custom domain). Follow the manual steps detailed above for those sections to complete the full scenario.
