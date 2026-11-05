# ADR-005: Satu VPS di belakang Caddy, dan dokumen yang dihasilkan dari kode

- Status: Diterima
- Tanggal: 2026-11-05

## Konteks
PRD meminta Docker Compose di satu VPS dengan Caddy dan HTTPS otomatis, berkas di Cloudflare R2, dan dokumen yang "tidak pernah
tertinggal dari kode". Membangun dan menguji stack di mesin pengembangan menunjukkan apa yang tidak ada di gambaran polos: batas
laju per alamat klien hanya melihat proxy, Keycloak butuh database yang hidup di luar containernya, dan realm yang bisa diimpor ke
database H2 pengembangan ternyata gagal diimpor ke SQL Server.

## Keputusan
- **Caddy** mengakhiri TLS untuk dua alamat: aplikasi (`/` ke container web, `/api`, `/hubs`, dan `/sitemap.xml` ke api) dan
  Keycloak di alamatnya sendiri, supaya cookie-nya terpisah dari aplikasi. Container web juga Caddy, menyajikan build dengan
  fallback single-page. Dashboard job, endpoint health, dan konsol admin Keycloak tidak di-route ke internet.
- **Keycloak menyimpan datanya di database kedua pada SQL Server Express yang sama** (dibuat sekali oleh service
  `keycloak-db-init`), bukan di berkas lokal container. Api meminta kunci ke Keycloak lewat jaringan internal (`MetadataAddress`),
  sementara token diterbitkan untuk alamat publik.
- **Api hanya memercayai `X-Forwarded-For` dan `X-Forwarded-Proto` dari jaringan yang dikonfigurasi** (pengaturan `ForwardedHeaders`;
  jaringan Docker pada deploy), sehingga batas laju per alamat melihat tiap pengunjung dan header palsu tidak berguna.
- **Compose produksi punya nama proyek sendiri**, jadi menjalankannya tidak pernah menyentuh volume stack pengembangan.
- **Dokumen yang diturunkan dari kode ditulis oleh `tools/SchoMans.DocGen`**, yang membangun api seperti `Program.cs` (dengan
  `EndpointRegistration` bersama) dan tidak butuh database. Ia menulis ERD (dari model EF Core), referensi API dan matriks otorisasi
  (dari endpoint dan policy terdaftar), job (dengan menjalankan registrasi asli terhadap pengelola job tiruan), dan event SignalR,
  serta mengisi region bertanda di system design. `--check` gagal bila berkas usang atau bila dokumen tulisan tangan menyebut rute
  API yang sudah tidak ada; `docs.yml` menjalankannya pada pull request (hanya drift) dan setelah merge (regenerasi, lalu buka pull request).
- **Screenshot dan alur ditangkap Playwright** terhadap alamat mana pun (`CAPTURE_URL`), ke `docs/assets/`; GIF alur dirakit dari
  gambar langkah dengan skrip Python kecil, karena ffmpeg bukan dependensi proyek.

## Alasan
- Satu developer bisa menjalankan, memperbarui, dan menjelaskan satu berkas compose; Caddy menghilangkan urusan sertifikat.
- Menghasilkan dokumen dari kode adalah satu-satunya cara agar klaim seperti "ERD mutakhir" bisa diperiksa di CI.
- Tiap keputusan di atas dipaksa oleh cacat yang ditemukan saat menguji stack, bukan oleh selera.

## Konsekuensi
- Deploy **diuji di mesin pengembangan** dengan nama `localhost`; DNS dan sertifikat sungguhan, Cloudflare R2, SMTP, dan server 4 GB
  di bawah beban belum diuji (disebutkan di [panduan deploy](../deployment.md)).
- Perubahan realm setelah start pertama butuh konsol Keycloak atau database baru, karena `--import-realm` melewati realm yang sudah ada.
- Backup belum otomatis.
- Referensi yang dihasilkan memuat endpoint dan tipenya, bukan skema tingkat bidang; dokumen OpenAPI dari api yang berjalan memilikinya.
