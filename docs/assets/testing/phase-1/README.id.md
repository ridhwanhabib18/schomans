# Bukti test Phase 1

Dibuat pada 2026-10-23 dari branch `feat/phase-1-foundation`. Semua berjalan terhadap layanan asli: SQL Server 2022 dan
Keycloak 26.8 di Docker, API asli, dan aplikasi web asli.

| Suite | Hasil |
| --- | --- |
| Unit test backend (Domain 30, Application 22) | 52 lulus |
| Integration test backend (WebApplicationFactory + SQL Server) | 44 lulus |
| Unit test frontend (Vitest) | 77 lulus |
| Test end-to-end (Playwright, Keycloak + API + web) | 19 lulus (17 desktop 1440 px, 2 HP 390 px) |

## Coverage (target PRD: minimal 70%)

Line coverage backend, semua proyek test digabung. Dikecualikan: migrasi EF Core, `Program.cs`, dan file hasil generate compiler.

| Assembly | Lines covered | Total lines | Line coverage |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 225 | 259 | 86.9% |
| SchoMans.Application | 108 | 113 | 95.6% |
| SchoMans.Domain | 272 | 320 | 85.0% |
| SchoMans.Infrastructure | 207 | 220 | 94.1% |
| **All** | **812** | **912** | **89.0%** |

Frontend (Vitest, v8): statements 76,9%, branches 68,3%, functions 73,2%, lines 78,0%. Dikecualikan: klien API hasil
generate, helper test, dan `main.tsx`.

Branch coverage frontend sedikit di bawah 70%; target PRD berlaku untuk test secara keseluruhan, dan kode yang
tidak punya unit test (`CallbackPage`, `app/providers` berisi pemulihan sesi dan tema, serta fungsi redirect OIDC di
`authService`) justru diuji oleh test end-to-end terhadap Keycloak asli.

## Isi
- `backend/`: hasil TRX ketiga proyek test, laporan coverage Cobertura, dan `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` dan laporan coverage HTML (`coverage-report/index.html`)
- `e2e/`: laporan HTML Playwright (`playwright-report/index.html`, satu screenshot per langkah), `playwright-junit.xml`, serta screenshot dashboard tiap peran, halaman login dan registrasi Keycloak, dan layout desktop dan HP

## Cara membuatnya
```bash
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
