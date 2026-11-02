using Microsoft.AspNetCore.Http;

/// How every service hands a stored file back: the type comes from the file name's extension (never from what the
/// uploader's browser claimed), the browser may not guess another (nosniff), and the response is sandboxed, so a file
/// opened straight from the API runs nothing. The screens read files with the sign-in token and show only what the
/// file's own first bytes say it is (shared/ui/file-viewer).
public static class SafeFiles
{
  private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
  {
    [".pdf"] = "application/pdf",
    [".jpg"] = "image/jpeg",
    [".jpeg"] = "image/jpeg",
    [".png"] = "image/png",
    [".webp"] = "image/webp",
    [".gif"] = "image/gif",
    [".tif"] = "image/tiff",
    [".tiff"] = "image/tiff",
    [".doc"] = "application/msword",
    [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    [".xls"] = "application/vnd.ms-excel",
    [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    [".csv"] = "text/csv",
    [".txt"] = "text/plain",
  };

  /// The type a file is served as, by its extension; anything unknown is plain bytes.
  public static string ContentTypeOf(string? fileName) =>
      Types.TryGetValue(Path.GetExtension(fileName ?? string.Empty), out var type) ? type : "application/octet-stream";

  /// The file under its name, its type from that name, with the protective headers.
  public static IResult File(HttpResponse response, Stream content, string fileName)
  {
    Protect(response);
    return Results.File(content, ContentTypeOf(fileName), fileName);
  }

  /// Only the headers - for a file that keeps its own vetted type (a letterhead logo).
  public static void Protect(HttpResponse response)
  {
    response.Headers.XContentTypeOptions = "nosniff";
    response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";
  }
}
