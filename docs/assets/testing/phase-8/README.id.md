# Bukti pengujian Fase 8

Dibuat pada 2026-11-05 dari branch `feat/phase-8-portfolio-release`. Semuanya berjalan terhadap layanan nyata di mesin pengembangan: SQL Server 2022, Keycloak 26.8, dan SeaweedFS 4.50 di Docker, API asli, dan aplikasi web asli; penyedia AI adalah mock yang deterministik (ADR-004). Kriteria selesai fase di PRD: "Tautan live + repo publik siap dibagikan."
**Kriteria itu belum terpenuhi: belum ada yang dideploy** (belum ada VPS atau domain). Yang siap adalah semua yang dibutuhkan untuk deploy (`docs/id/deployment.md`), diuji lokal sejauh yang bisa dilakukan mesin pengembangan.

| Suite | Hasil |
| --- | --- |
| Tes unit backend (Domain 220, Application 350) | 570 lolos |
| Tes integrasi backend (WebApplicationFactory + SQL Server + SeaweedFS) | 724 lolos |
| Tes unit frontend (Vitest) | 711 lolos (62 file) |
| Tes end-to-end (Playwright, 56 tes, satu kali jalan tanpa putus, 12,6 menit) | 56 lolos |
| Skrip tangkapan terhadap stack lokal (7 tangkapan halaman, 5 alur) | 12 lolos |
| Pemeriksaan dokumentasi (`SchoMans.DocGen --check`) | mutakhir, tidak ada diagram yang menyebut rute yang sudah hilang |

Total backend: 1294 tes (3 baru: `ForwardedHeadersTests`).

## Cakupan (target PRD: minimal 70%)
Cakupan baris backend, semua proyek tes digabung. Dikecualikan: migrasi EF Core, `Program.cs`, dan berkas buatan compiler. `tools/SchoMans.DocGen` tidak punya tes sendiri; ia dijalankan oleh pemeriksaan dokumentasi.

| Assembly | Baris tercakup | Total baris | Cakupan baris |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 1438 | 1533 | 93,8% |
| SchoMans.Application | 7674 | 8247 | 93,1% |
| SchoMans.Domain | 2584 | 2922 | 88,4% |
| SchoMans.Infrastructure | 1901 | 2060 | 92,3% |
| **Semua** | **13597** | **14762** | **92,1%** |

Frontend (Vitest, v8): statements 90,67%, branches 86,53%, functions 85,94%, lines 91,5%. Dikecualikan: klien API hasil generate, helper tes, dan `main.tsx`.

## Lighthouse pada halaman publik (target PRD: 90)
Lighthouse 12, profil mobile (4G lambat simulasi, CPU 4× lebih lambat), Chromium headless, build produksi disajikan `vite preview` dengan API di mesin yang sama (jadi tanpa kompresi atau HTTP/2 dari proxy sungguhan). Laporan: `lighthouse/*.report.html`.

| Halaman | Performa | Aksesibilitas | Best practices | SEO | Paint pertama | Paint terbesar | Waktu blokir |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Halaman depan `/` | 46 → **69** | 99 | 93 | 91 → **100** | 3,7 → 2,4 dtk | 5,6 → 2,4 dtk | 990 → 1110 ms |
| Daftar berita `/news` | 57 → **75** | 98 | 93 | 91 → **100** | 3,7 → 2,3 dtk | 4,5 → 2,9 dtk | 770 → 680 ms |
| Satu artikel | 55 → **78** | 99 | 93 | 91 → **100** | 3,7 → 2,3 dtk | 4,6 → 3,1 dtk | 920 → 550 ms |

(Angka pertama sebelum perubahan fase ini, angka kedua sesudahnya.) **Performa di bawah target 90 pada ketiga halaman.** Dua perubahan dilakukan: rute area setelah login dimuat saat dibuka (aplikasi tadinya satu bundel 1 MB), dan `robots.txt` ditambahkan (server menjawab dengan aplikasi, yang oleh Lighthouse dihitung 18 kesalahan). Yang tersisa adalah JavaScript aplikasi single-page itu sendiri (bundel utama sekitar 490 KB, waktu blokir 0,5 sampai 1,1 detik pada CPU 4× lebih lambat), dan halaman depan masih sedikit bergeser tata letaknya (0,108) saat berita tiba. Mencapai 90 butuh pre-render halaman publik, memuat SignalR dan pustaka login hanya setelah login, dan menyediakan ruang untuk daftar berita; itu belum dikerjakan.

## Yang dibuktikan tes dan pemeriksaan
- **Forwarded headers** (`ForwardedHeadersTests`): di balik proxy tepercaya setiap pengunjung punya batasnya sendiri (6 pertanyaan chatbot per menit); sambungan yang bukan proxy tepercaya tidak bisa mengubah alamatnya dengan `X-Forwarded-For` palsu; dengan pengaturan mati, header diabaikan. Hal yang sama diperiksa terhadap stack nyata lewat Caddy: 6 jawaban, lalu 429, apa pun alamat palsunya.
- **Berkas deploy**: kedua image terbangun (API 641 MB, web 94 MB); stack compose produksi start sehat di `localhost` dan `auth.localhost`; Caddy menyajikan aplikasi dengan deep link, cache panjang untuk berkas ber-hash, dan tanpa cache untuk `sw.js`; API menjawab lewat Caddy dengan token Keycloak; login demo lewat Keycloak asli dan setiap halaman peran guru berjalan; `/hangfire` dan konsol admin Keycloak tidak terjangkau dari luar. (Image dibangun dengan sertifikat proxy mesin ini ditambahkan ke Dockerfile sementara yang tidak ada di repositori.)
- **Dokumentasi dari kode**: 181 operasi di referensi API dan matriks otorisasi, ERD dan satu diagram per domain, job berulang (diambil dari registrasi asli), dan event SignalR. Pemeriksaan gagal bila berkas hasil usang atau bila dokumen tulisan tangan menyebut rute API yang tidak ada; rute karangan di diagram terdeteksi selama fase ini.
- **Galeri** (`docs/assets/screenshots/`, 166 gambar, dan `docs/assets/flows/`, 5 GIF): ditangkap dari database baru dengan data demo fiktif, dengan pengguna demo admin staf bernama M Ridhwan Habib A.

## Ditemukan oleh pengujian di fase ini
1. Di balik proxy, batas laju per alamat klien akan menaruh semua pengunjung di satu keranjang (diperbaiki: `ForwardedHeaders`).
2. API menolak alamat kunci HTTP biasa di jaringan Docker internal (diperbaiki: `Keycloak__RequireHttpsMetadata` di compose produksi).
3. Impor realm Keycloak gagal di SQL Server: deskripsi satu client lebih dari 255 karakter (database H2 pengembangan tidak menegakkannya). Diperbaiki; Keycloak sebelumnya restart 15 kali.
4. Proyek yang hanya mereferensikan API me-resolve `Newtonsoft.Json` ke versi dengan kerentanan yang diketahui (diperbaiki dengan pin versi ke 13.0.4 yang sudah dipakai API).
5. Tidak ada `robots.txt` (diperbaiki).
6. Dua cacat end-to-end pada tes, bukan produk: navigasi kedua yang dilakukan tepat setelah navigasi lain dibatalkan selagi halaman rute lazy masih dimuat (spec absensi kini menunggu halamannya), dan `getByLabel('Feedback')` cocok dengan dua elemen setelah region umpan balik AI termuat (dibuat persis).

## Penyimpangan dari PRD dan keputusan yang perlu dikonfirmasi
1. **Belum dideploy.** Tidak ada VPS atau domain, jadi tidak ada tautan live, dan galeri berasal dari stack lokal. `CAPTURE_URL=<url demo> npm run capture:screenshots` mengulanginya terhadap demo. Panduan deploy menyebut apa yang sudah dan belum diuji.
2. **GIF alur** dirakit dari gambar langkah dengan skrip Python kecil (Pillow), karena ffmpeg tidak terpasang di sini dan bukan dependensi proyek. GIF PPDB hanya tiga langkah: database baru belum punya pelamar.
3. **Video demo 2 menit** berupa naskah untuk direkam (`docs/id/demo-video-script.md`), bukan video.
4. **Referensi API**: dihasilkan dari endpoint terdaftar (method, rute, nama, policy, tipe request dan response), bukan dari `openapi.json` hasil ekspor; skema tingkat bidang ada di dokumen OpenAPI dari API yang berjalan.
5. **Belum dibangun dari tabel stack**: Seq (log ke konsol) dan backup otomatis.
6. **Performa Lighthouse** di bawah 90 (lihat di atas).
7. **Seed demo tidak punya riwayat nilai atau absensi**, jadi dashboard database baru menampilkan grafik "no data". Menambah riwayat ke seed adalah tindak lanjut; tidak ada di PRD.
8. **Nama demo**: pengguna demo admin staf, sebelumnya Sari Utami, kini M Ridhwan Habib A (seed, realm Keycloak, dan tes yang menyebut namanya).
9. **Pin dependensi**: `Newtonsoft.Json` 13.0.4 di `Directory.Packages.props`. Tidak ada paket yang ditambahkan.
10. **Sinkron tab System Design** tidak berlaku: dokumen design adalah berkas Markdown di repositori.

## Masalah yang diketahui
- Suite end-to-end peka terhadap mesin yang kehabisan memori. Dengan sekitar 200 MB memori bebas satu run memakan 18 menit dan satu tes real-time kena timeout; spec yang sama lolos sendirian, dan run akhir 56 tes tanpa putus. Demikian pula, satu run Vitest tidak bisa memulai tiga worker (`Timeout waiting for worker`) dan hanya menjalankan 688 dari 711 tes; run yang disimpan di sini menjalankan semua 711.
- `AchievementDescriptionEndpointTests.A_bulk_run_...` (AI-02) pernah gagal sekali di fase sebelumnya dan lolos sejak itu; penyebabnya belum ditemukan.
- Klien API hasil generate masih ditulis tangan untuk endpoint Fase 4 dan seterusnya.
- Belum ada layar untuk jatah AI pengguna (`AiGateway.GetAllowanceAsync` ada, endpoint belum).

## Data yang ditinggalkan tes
Tes end-to-end meninggalkan baris di database pengembangan (seperti fase sebelumnya). Skrip tangkapan memakai database sendiri, `SchoMansCapture`, yang boleh dihapus kapan saja. Tes end-to-end kuis meninggalkan dua soal di bank soal.

## Isi
- `backend/`: hasil TRX tiga proyek tes, laporan Cobertura, dan `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` dan laporan cakupan HTML (`coverage-report/index.html`)
- `e2e/`: laporan HTML Playwright (`playwright-report-run-1/index.html`) dan log teks run (`logs/`)
- `lighthouse/`: laporan HTML dan JSON tiga halaman publik

## Cara membuatnya
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx        # backend (hentikan API yang berjalan dulu: ia mengunci DLL)
cd web && npm run test:coverage                                 # frontend
cd web && npm run test:e2e                                      # end to end
dotnet run --project tools/SchoMans.DocGen -- --check           # dokumen
cd web && npm run capture:screenshots && npm run capture:flows  # galeri
npx lighthouse@12 http://localhost:4173/ --output=json --output=html   # setelah npm run build dan vite preview
```
