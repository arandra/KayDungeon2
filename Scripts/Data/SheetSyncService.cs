using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public static class SheetSyncService
{
    private const string ConfigPath = "res://Data/Sheets/sheet_config.json";
    private const string SheetsScope = "https://www.googleapis.com/auth/spreadsheets.readonly";

    public static bool SyncFromGoogle(out string message)
    {
        try
        {
            if (!FileAccess.FileExists(ConfigPath))
            {
                message = $"SheetSyncService: Config not found: {ConfigPath}";
                return false;
            }

            var serviceAccountPath = SheetUserSettings.LoadServiceAccountPath();
            if (string.IsNullOrWhiteSpace(serviceAccountPath) || !File.Exists(serviceAccountPath))
            {
                message = "SheetSyncService: Service account key path is missing or invalid.";
                return false;
            }

            var config = LoadConfig(ConfigPath);
            if (config == null || string.IsNullOrWhiteSpace(config.SpreadsheetId))
            {
                message = "SheetSyncService: Invalid config or spreadsheetId missing.";
                return false;
            }

            var serviceAccount = LoadServiceAccount(serviceAccountPath);
            if (serviceAccount == null)
            {
                message = "SheetSyncService: Failed to parse service account JSON.";
                return false;
            }

            var accessToken = RequestAccessToken(serviceAccount);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                message = "SheetSyncService: Failed to obtain access token.";
                return false;
            }

            var outputDir = ProjectSettings.GlobalizePath("res://Data/Sheets");
            Directory.CreateDirectory(outputDir);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            foreach (var sheet in config.Sheets)
            {
                if (string.IsNullOrWhiteSpace(sheet.Range) || string.IsNullOrWhiteSpace(sheet.Output))
                {
                    continue;
                }

                var table = FetchSheetTable(client, config.SpreadsheetId, sheet);
                if (table == null)
                {
                    GD.PushWarning($"SheetSyncService: Failed to fetch {sheet.Name}");
                    continue;
                }

                var json = JsonSerializer.Serialize(table, new JsonSerializerOptions { WriteIndented = true });
                var outputPath = Path.Combine(outputDir, sheet.Output);
                File.WriteAllText(outputPath, json, Encoding.UTF8);
            }

            message = "SheetSyncService: Sync complete.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"SheetSyncService: Sync failed: {ex.Message}";
            return false;
        }
    }

    private static SheetConfig LoadConfig(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        var json = file.GetAsText();
        return JsonSerializer.Deserialize<SheetConfig>(json);
    }

    private static ServiceAccountInfo LoadServiceAccount(string path)
    {
        var json = File.ReadAllText(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<ServiceAccountInfo>(json);
    }

    private static string RequestAccessToken(ServiceAccountInfo account)
    {
        if (account == null)
        {
            return "";
        }

        var tokenUri = string.IsNullOrWhiteSpace(account.TokenUri) ? "https://oauth2.googleapis.com/token" : account.TokenUri;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = new Dictionary<string, object>
        {
            ["iss"] = account.ClientEmail,
            ["scope"] = SheetsScope,
            ["aud"] = tokenUri,
            ["iat"] = now,
            ["exp"] = now + 3600
        };

        var jwt = CreateSignedJwt(payload, account.PrivateKey);
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return "";
        }

        using var client = new HttpClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = jwt
        });

        var response = client.PostAsync(tokenUri, content).Result;
        if (!response.IsSuccessStatusCode)
        {
            return "";
        }

        var responseJson = response.Content.ReadAsStringAsync().Result;
        using var doc = JsonDocument.Parse(responseJson);
        if (!doc.RootElement.TryGetProperty("access_token", out var tokenElement))
        {
            return "";
        }

        return tokenElement.GetString();
    }

    private static string CreateSignedJwt(Dictionary<string, object> payload, string privateKeyPem)
    {
        if (string.IsNullOrWhiteSpace(privateKeyPem))
        {
            return "";
        }

        var header = new Dictionary<string, object>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT"
        };

        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);
        var headerEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var unsignedToken = $"{headerEncoded}.{payloadEncoded}";

        using var rsa = RSA.Create();
        var normalizedKey = privateKeyPem.Replace("\\n", "\n");
        rsa.ImportFromPem(normalizedKey);
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(unsignedToken), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureEncoded = Base64UrlEncode(signature);

        return $"{unsignedToken}.{signatureEncoded}";
    }

    private static SheetTable FetchSheetTable(HttpClient client, string spreadsheetId, SheetDefinition sheet)
    {
        var rangeEscaped = Uri.EscapeDataString(sheet.Range);
        var url = $"https://sheets.googleapis.com/v4/spreadsheets/{spreadsheetId}/values/{rangeEscaped}?majorDimension=ROWS";

        var response = client.GetAsync(url).Result;
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = response.Content.ReadAsStringAsync().Result;
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("values", out var valuesElement))
        {
            return null;
        }

        var values = new List<List<string>>();
        foreach (var rowElement in valuesElement.EnumerateArray())
        {
            var row = new List<string>();
            foreach (var cell in rowElement.EnumerateArray())
            {
                row.Add(cell.GetString() ?? "");
            }
            values.Add(row);
        }

        if (values.Count == 0)
        {
            return null;
        }

        var headers = values[0];
        var rows = new List<List<string>>();
        for (int i = 1; i < values.Count; i++)
        {
            rows.Add(values[i]);
        }

        return new SheetTable
        {
            Name = sheet.Name,
            Headers = headers,
            Rows = rows
        };
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private class ServiceAccountInfo
    {
        public string ClientEmail { get; set; } = "";
        public string PrivateKey { get; set; } = "";
        public string TokenUri { get; set; } = "";

        public string client_email
        {
            get => ClientEmail;
            set => ClientEmail = value;
        }

        public string private_key
        {
            get => PrivateKey;
            set => PrivateKey = value;
        }

        public string token_uri
        {
            get => TokenUri;
            set => TokenUri = value;
        }
    }
}
