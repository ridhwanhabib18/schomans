# ADR-002: Memakai Minimal API, bukan Controllers

- Status: Diterima
- Tanggal: 2026-10-23

## Konteks
ASP.NET Core menyediakan dua gaya endpoint: Controllers (MVC) dan Minimal API. Banyak codebase
enterprise masih memakai Controllers, sementara Microsoft mendorong Minimal API untuk proyek baru.

## Keputusan
SchoMans memakai Minimal API dengan satu file endpoint per fitur.

## Alasan
- Selaras dengan susunan per fitur dan pemanggilan handler langsung (ADR-001); controller hanya
  akan menjadi lapisan penerus.
- Dependency di-inject per endpoint, bukan ke seluruh class.
- Endpoint filter cocok untuk validasi tanpa pipeline mediator.
- Lebih ringan, dengan OpenAPI bawaan untuk generate client TypeScript orval.

## Konsekuensi
- Struktur harus dijaga lewat konvensi (lihat `CLAUDE.md` dan PRD bagian 7).
- Fitur MVC seperti `ModelState` dan content negotiation tidak tersedia; tidak dibutuhkan karena
  API hanya JSON.
- Library yang hanya mendukung controller tidak bisa dipakai.

## Kekurangan dan cara mengatasi
| Kekurangan | Cara mengatasi |
| --- | --- |
| Rawan berantakan | Satu file per fitur + `MapGroup`, tidak ada endpoint di `Program.cs` |
| Lambda sulit dibaca dan di-debug | Method static bernama, tanpa logika bisnis di endpoint |
| Binding parameter kadang mengejutkan | Sumber eksplisit, `[AsParameters]` untuk query |
| `IFormFile` terkena antiforgery | `.DisableAntiforgery()` (JWT, bukan cookie) |
| Fitur MVC lebih sedikit | Library wajib mendukung Minimal API |
| Metadata endpoint panjang | Konfigurasi di level group, `.WithValidation<T>()` |
| Unit test endpoint sulit | Unit test di handler, integration test per endpoint |
| Familiaritas tim | ADR ini |

## Alternatif yang ditolak
- Controllers: boilerplate lebih banyak tanpa manfaat tambahan untuk proyek ini.
- FastEndpoints atau Carter: menambah dependency dan pola baru di atas Minimal API.
