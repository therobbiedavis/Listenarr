As a security-aware developer, generate secure .NET code using ASP.NET Core that inherently prevents top security weaknesses.
Focus on making the implementation inherently safe rather than merely renaming methods with "secure_" prefixes.
Use inline comments to clearly highlight critical security controls, implemented measures, and any security assumptions made in the code.
Adhere strictly to best practices from OWASP, with particular consideration for the OWASP ASVS guidelines.
**Avoid Slopsquatting**: Be careful when referencing or importing packages. Do not guess if a package exists. Comment on any low reputation or uncommon packages you have included.

### CWE-79: Improper Neutralization of Input During Web Page Generation ('Cross-site Scripting')
**Summary:** The software does not neutralize or incorrectly neutralizes user-controllable input before it is placed in an output that is used as a web page, leading to the execution of arbitrary script code in a user's browser.
**Mitigation Rule:** Always encode all user-supplied input when rendering it in HTML to prevent XSS. Utilize ASP.NET Core Razor's automatic HTML encoding for all content displayed via `@` syntax or `IHtmlContent`, and explicitly use `HtmlEncoder.Default.Encode` for custom scenarios where auto-encoding might be bypassed or if the content is sourced from untrusted external systems. For Blazor, leverage its automatic output encoding mechanisms for rendered UI.

### CWE-89: Improper Neutralization of Special Elements used in an SQL Command ('SQL Injection')
**Summary:** The software constructs all or part of an SQL command using externally-controlled input, but it does not neutralize or incorrectly neutralizes special elements that can modify the intended SQL command when it is parsed.
**Mitigation Rule:** Always use parameterized queries or Object-Relational Mappers (ORMs) like Entity Framework Core (EF Core) for all database interactions. Avoid string concatenation to build SQL queries, even for WHERE clauses or order by clauses, and never embed raw user input directly into SQL statements.

### CWE-352: Cross-Site Request Forgery (CSRF)
**Summary:** The web application does not sufficiently protect sensitive functionality against Cross-Site Request Forgery attacks, allowing an attacker to trick a user into performing an unintended action.
**Mitigation Rule:** Implement ASP.NET Core's built-in Anti-Forgery features by adding the `[ValidateAntiForgeryToken]` attribute to all POST, PUT, and DELETE actions that modify data, and include the anti-forgery token in all relevant forms. For AJAX calls, ensure the anti-forgery token is included in request headers.

### CWE-522: Insufficiently Protected Credentials
**Summary:** The software stores or transmits credentials in a way that allows them to be retrieved or intercepted by an unauthorized actor.
**Mitigation Rule:** Never hardcode secrets, API keys, connection strings, or sensitive configurations directly into the source code. Utilize ASP.NET Core's `IConfiguration` abstraction with secure providers such as Azure Key Vault, environment variables, `dotnet user-secrets` for development, or a secure secrets management solution for production.

### CWE-918: Server-Side Request Forgery (SSRF)
**Summary:** The web server accepts a URL or similar request from an untrusted source and uses it to access another resource without sufficiently validating that the URL is for an appropriate resource.
**Mitigation Rule:** When an application accepts a URL or target from user input, strictly validate and sanitize the input against an allow-list of approved domains or IP ranges. When using `HttpClient` or similar classes, implement a robust allow-list, use network segmentation, and consider disabling automatic redirects. Always use a secure HTTP client that respects proxy settings and does not follow redirects to internal resources.

### CWE-22: Improper Limitation of a Pathname to a Restricted Directory ('Path Traversal')
**Summary:** The software uses external input to construct a pathname that is intended to identify a file or directory that is located underneath a restricted parent directory, but the software does not properly neutralize special elements within the pathname that can cause the pathname to resolve to a location outside of the restricted directory.
**Mitigation Rule:** When handling file paths based on user input, always use `Path.Combine` to construct paths and then canonicalize them using `Path.GetFullPath` to resolve any directory traversal sequences (e.g., `../`). Afterward, rigorously validate that the canonicalized path remains within an explicitly defined and restricted base directory using `StartsWith` or `IsSubPathOf`.

### CWE-287: Improper Authentication
**Summary:** The software does not perform a sufficient check to confirm the identity of a user or system, or the check can be bypassed by an attacker.
**Mitigation Rule:** Leverage ASP.NET Core Identity for robust authentication management, ensuring strong password policies, multi-factor authentication (MFA) capabilities, secure password hashing (e.g., PBKDF2 with a high iteration count, or Argon2), and proper account lockout mechanisms. Securely manage session tokens and cookies using industry standards like OAuth 2.0 and OpenID Connect, ensuring secure flag settings (`HttpOnly`, `Secure`, `SameSite`).