using Microsoft.AspNetCore.Http;

/// How every service hands a stored file back: its type comes from the file name's extension (never from what the
/// uploader's browser claimed), the browser is told not to guess another type (nosniff), and the response is sandboxed
/// (CSP sandbox), so a file opened from the API cannot run script in the ERP's origin. The screens fetch files with the
/// sign-in token and show only PDFs and pictures themselves.
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

  /// The file with its original name, its type from that name, and the protective headers.
  public static IResult File(HttpResponse response, Stream content, string fileName)
  {
    Protect(response);
    return Results.File(content, ContentTypeOf(fileName), fileName);
  }

  /// Only the protective headers - for a file that keeps its own vetted type (a letterhead logo shown in an img tag).
  public static void Protect(HttpResponse response)
  {
    ArgumentNullException.ThrowIfNull(response);
    response.Headers.XContentTypeOptions = "nosniff";
    response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'; img-src 'self' data:; style-src 'unsafe-inline'";
  }
}
