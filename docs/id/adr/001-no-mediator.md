# ADR-001: Tidak memakai pattern mediator

- Status: Diterima
- Tanggal: 2026-10-23

## Konteks
Template Clean Architecture .NET umumnya memakai CQRS + MediatR. Sejak 2025 MediatR berlisensi
komersial, dan banyak tim mulai mempertanyakan perlunya lapisan mediator.

## Keputusan
SchoMans tidak memakai pattern mediator maupun library MediatR atau Mediator. Endpoint memanggil
handler secara langsung lewat dependency injection.

## Alasan
- Setiap command hanya punya satu handler, jadi lapisan mediator tidak memberi manfaat nyata.
- Kebutuhan lintas fitur sudah tercakup mekanisme bawaan ASP.NET Core dan EF Core.
- Alur kode eksplisit: *go to definition* langsung dari endpoint ke handler.
- Bebas dari risiko lisensi komersial.

## Pengganti pipeline
| Kebutuhan | Mekanisme |
| --- | --- |
| Validasi | Endpoint filter FluentValidation (`WithValidation<T>()`) |
| Audit log | `SaveChangesInterceptor` EF Core |
| Logging & error | Middleware Serilog + ProblemDetails |

## Konsekuensi
Endpoint bergantung pada tipe handler konkret; dapat diterima untuk proyek satu tim dan satu
deployable. Penamaan Command/Handler tetap dipakai untuk mengorganisasi use case.

## Alternatif yang ditolak
- MediatR: lisensi komersial.
- Mediator (MIT): menambah lapisan tanpa manfaat sepadan; dipertimbangkan ulang jika proyek butuh
  banyak handler per event atau pipeline yang kompleks.
