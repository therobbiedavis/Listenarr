/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using Microsoft.AspNetCore.Mvc;

namespace Listenarr.Api.Controllers;

// Audible library controller removed; keep placeholder endpoints returning 404
[ApiController]
[Route("api/audible-library")]
public class AudibleLibraryController : ControllerBase
{
    [HttpGet]
    public IActionResult GetLibrary() => NotFound(new { message = "Audible integration removed" });

    [HttpGet("book/{asin}")]
    public IActionResult GetLibraryBook(string asin) => NotFound(new { message = "Audible integration removed" });

    [HttpGet("catalog/{asin}")]
    public IActionResult GetCatalogBook(string asin) => NotFound(new { message = "Audible integration removed" });
}
