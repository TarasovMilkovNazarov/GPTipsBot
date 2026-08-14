using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace GPTipsBot.Web;

public static class SeoEndpoints
{
    internal const string RobotsTxt =
        """
        User-agent: *
        Allow: /
        Disallow: /api/
        Disallow: /webhooks/
        Disallow: /health

        Sitemap: https://gptips.skolkokomu.ru/sitemap.xml
        """;

    internal const string SitemapXml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
          <url>
            <loc>https://gptips.skolkokomu.ru/</loc>
            <lastmod>2026-08-14</lastmod>
            <changefreq>weekly</changefreq>
            <priority>1.0</priority>
          </url>
        </urlset>
        """;

    public static void MapSeoFiles(this WebApplication app)
    {
        app.MapMethods("/robots.txt", ["GET", "HEAD"], () =>
            Results.Text(RobotsTxt + "\n", "text/plain; charset=utf-8"));
        app.MapMethods("/sitemap.xml", ["GET", "HEAD"], () =>
            Results.Text(SitemapXml + "\n", "application/xml; charset=utf-8"));
    }
}
