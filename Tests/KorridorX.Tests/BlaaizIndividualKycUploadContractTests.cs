using System.Text.Json;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using Xunit;

namespace KorridorX.Tests;

public sealed class BlaaizIndividualKycUploadContractTests
{
    [Fact]
    public void Observed_array_headers_are_normalized_and_Host_is_not_returned()
    {
        const string json = """
        {
          "message": "Upload URL generated successfully",
          "file_id": "file-123",
          "url": "https://upload.example.test/object?X-Amz-Signature=private-signature",
          "headers": {
            "Host": ["upload.example.test"],
            "Content-Type": ["application/pdf"],
            "x-amz-acl": "private",
            "x-amz-meta-tags": ["first", "second"]
          }
        }
        """;

        var response = JsonSerializer.Deserialize<BlaaizKycUploadUrlResponse>(json);

        Assert.NotNull(response);
        Assert.Equal("file-123", response!.FileId);
        Assert.Equal(
            "https://upload.example.test/object?X-Amz-Signature=private-signature",
            response.Url);
        Assert.False(response.Headers.ContainsKey("Host"));
        Assert.Equal("application/pdf", response.Headers["Content-Type"]);
        Assert.Equal("private", response.Headers["x-amz-acl"]);
        Assert.Equal("first,second", response.Headers["x-amz-meta-tags"]);
    }

    [Fact]
    public void Documented_nested_upload_response_is_supported()
    {
        const string json = """
        {
          "message": "Upload URL generated successfully",
          "data": {
            "file_id": "file-456",
            "url": "https://upload.example.test/file",
            "headers": {
              "Host": "upload.example.test",
              "x-amz-acl": "private"
            }
          }
        }
        """;

        var response = JsonSerializer.Deserialize<BlaaizKycUploadUrlResponse>(json);

        Assert.NotNull(response);
        Assert.Equal("file-456", response!.FileId);
        Assert.Equal("https://upload.example.test/file", response.Url);
        Assert.False(response.Headers.ContainsKey("Host"));
        Assert.Equal("private", response.Headers["x-amz-acl"]);
    }

    [Theory]
    [InlineData("""{"Host":["wrong.example.test"]}""")]
    [InlineData("""{"Host":["upload.example.test","wrong.example.test"]}""")]
    [InlineData("""{"x-amz-acl":[]}""")]
    [InlineData("""{"x-amz-acl":["private",4]}""")]
    [InlineData("""{"x-amz-acl":null}""")]
    [InlineData("""{"x-amz-acl":true}""")]
    public void Invalid_upload_header_contracts_remain_rejected(string headers)
    {
        var json =
            """{"file_id":"file-123","url":"https://upload.example.test/file","headers":""" +
            headers +
            "}";

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<BlaaizKycUploadUrlResponse>(json));
    }
}