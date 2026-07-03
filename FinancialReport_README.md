# Financial Report Generation

This document explains how to work with the Financial Report generation feature in the Database Backup API.

## Overview
The application provides an endpoint to generate a comprehensive financial report based on the database records. The report is generated as an HTML file and is automatically saved to your Desktop.

It pulls data from the following tables:
- `Messes`
- `Users`
- `Meals`
- `Deposits`
- `BazarCosts`

## How to Generate the Report

To generate the financial report, you need to make a `GET` request to the following API endpoint:

```
GET /api/backup/financial-report
```

### Expected Response

If successful, the API will return a JSON response containing the path to the newly generated HTML file:

```json
{
  "message": "Financial report generated successfully!",
  "filePath": "C:\\Users\\<YourUsername>\\Desktop\\FinancialReport_20260703_142500.html",
  "tables": [
    "Users",
    "Meals",
    "Deposits",
    "BazarCosts"
  ]
}
```

The generated HTML file will be saved directly to your machine's Desktop.

### Troubleshooting

- **Database Connection Error**: Make sure your `appsettings.json` has a valid `NeonConnection` connection string.
- **Missing Tables**: If the required tables are missing in the database, the API may fail to generate the report. It is recommended to check the database schema.
- **Permissions Issue**: Ensure the application has the necessary permissions to write files to your Desktop.
