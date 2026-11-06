# AgriStream IoT

Smart farm management platform for the CLDV7112 Cloud Development B practicum.
Author: Genius Mhirizhonga (ST10541838)

Live site: https://st10541838-agristream.azurewebsites.net

## What it does

- MVC web app on Azure App Service with Farmer, Agronomist and Admin roles
- Users, roles and the device registry in Azure SQL Database
- Drone images in Azure Blob Storage, sensor telemetry in Azure Table Storage
- A pipeline of exactly four Azure Functions: Blob trigger, HTTP GET, HTTP POST and Queue trigger
- Auto-scale from 1 to 3 instances on CPU, monitored with Application Insights

## Project layout

    src/AgriStream.Web         MVC application (.NET 10)
    src/AgriStream.Functions   Azure Functions, isolated worker (.NET 8)
    loadtest                   Load test and pipeline burst scripts (Node.js)
    samples                    Generated sample drone images for testing

## Configuration

Secrets are held in Azure application settings and are never committed.

Web app: `ConnectionStrings__SqlDatabase`, `ConnectionStrings__AzureStorage`, `Functions__BaseUrl`,
`Functions__Key`, `LoadTest__Key`, `Seed__FarmerPassword`, `Seed__AgronomistPassword`, `Seed__AdminPassword`,
`APPLICATIONINSIGHTS_CONNECTION_STRING`

Function app: `StorageConnection`, `SqlConnection`, `APPLICATIONINSIGHTS_CONNECTION_STRING`

## Load testing

    BASE_URL=https://st10541838-agristream.azurewebsites.net LOAD_KEY=... FARMER_PW=... node loadtest/loadtest.js
    FUNC_URL=https://func-agristream-st10541838.azurewebsites.net FUNC_KEY=... node loadtest/pipeline-burst.js
