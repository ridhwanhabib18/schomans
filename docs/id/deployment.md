# Panduan deploy

Cara menaruh SchoMans di satu VPS dengan HTTPS. Semuanya ada di `infra/`: `docker-compose.prod.yml`, `Caddyfile`, `.env.production.example`, `make-production-realm.sh`, `sqlserver/init-keycloak.sql`, dan Dockerfile untuk api (`src/SchoMans.Api/Dockerfile`) dan aplikasi web (`web/Dockerfile`). Rancangannya ada di bagian 3 [system design](../en/system-design.md) (bahasa Inggris).

## Yang dibutuhkan
- VPS dengan sekitar **4 GB RAM** (SQL Server Express sampai 1,5 GB, Keycloak sekitar 1 GB, api sampai 0,5 GB), Docker Engine dengan plugin compose, dan port **80 dan 443** terbuka ke internet.
- **Dua record DNS** (A atau AAAA) ke server: satu untuk aplikasi (misalnya `school.example.com`) dan satu untuk Keycloak (`auth.school.example.com`). Caddy meminta sertifikat HTTPS saat pertama kali melihat tiap nama, jadi record harus sudah ada sebelum start pertama.
- Akun **Cloudflare R2** untuk berkas: bucket `schomans-private` dan `schomans-public`, token API dengan akses baca dan tulis ke keduanya, dan aturan CORS di kedua bucket yang mengizinkan `GET` dan `PUT` dari `https://school.example.com` (browser mengunggah langsung ke R2 dengan presigned URL). Pakai endpoint S3 `https://<account-id>.r2.cloudflarestorage.com` dan alamat publik `schomans-public` untuk `R2_PUBLIC_BASE_URL`.
- Opsional: akun SMTP (tanpa itu email menunggu di antrean), dan key Claude API (tanpa itu fitur AI memakai penyedia mock yang gratis, lihat ADR-004).

## Langkah
```bash
git clone https://github.com/ridhwanhabib18/schomans.git && cd schomans
cp infra/.env.production.example infra/.env.production
nano infra/.env.production            # isi semua nilai; pilih kata sandi acak yang panjang
sh infra/make-production-realm.sh     # menulis infra/keycloak/realm-schomans.production.json
docker compose -f infra/docker-compose.prod.yml --env-file infra/.env.production up -d --build
```
Start pertama membangun dua image dan butuh beberapa menit; Keycloak butuh satu sampai dua menit lagi untuk mengimpor realm. Lalu periksa:
- `https://school.example.com` menampilkan halaman depan, dan `https://school.example.com/api/v1/news/public` menjawab `200`.
- `https://auth.school.example.com/realms/schomans/.well-known/openid-configuration` memuat issuer `https://auth.school.example.com/realms/schomans`.
- Dengan `DEMO_MODE=true` halaman login punya tombol demo satu klik; api memigrasi database dan mengisi sekolah fiktif pada start pertamanya (`SEED_DEMO_DATA=true`).

Untuk memperbarui: `git pull`, jalankan `make-production-realm.sh` lagi bila realm berubah, lalu jalankan `up -d --build` yang sama. Realm hanya diimpor saat Keycloak start dengan database kosong, jadi perubahan pada `realm-schomans.json` butuh konsol admin Keycloak atau database baru agar berlaku.

## Apa yang terjangkau dari internet, dan apa yang tidak
- Publik: aplikasi, `/api/*`, `/hubs/*`, `/sitemap.xml`, dan Keycloak di alamatnya sendiri.
- **Tidak di-route**: dashboard job (`/hangfire`), referensi API (`/scalar` dan dokumen OpenAPI hanya aktif di environment Development dan Demo, dan proxy tidak meneruskannya ke api), konsol admin Keycloak, SQL Server, dan endpoint health. Jangkau konsol admin dengan SSH tunnel ke container Keycloak, jangan dibuka.
- Api hanya memercayai `X-Forwarded-For` dan `X-Forwarded-Proto` dari jaringan Docker (pengaturan `ForwardedHeaders`), jadi batas laju per alamat klien (6 pertanyaan chatbot per menit per tamu) melihat tiap pengunjung, dan header palsu tidak berguna.
- **Akun demo memakai kata sandi publik** (`DEMO_PASSWORD`, yang memang ikut di bundel web). Pakai hanya dengan data fiktif. Deploy dengan orang sungguhan harus mengatur `DEMO_MODE=false` dan `SEED_DEMO_DATA=false`, menghapus client `schomans-web-demo` dan pengguna demo dari realm, lalu memakai login Keycloak.

## Gambar dari demo yang berjalan
Setelah deploy, dari `web/`: `CAPTURE_URL=https://school.example.com npm run capture:screenshots` mengunjungi setiap halaman setiap peran di ukuran desktop dan ponsel (ke `docs/assets/screenshots/`), dan `npm run capture:flows` merekam alur utama menjadi GIF (`docs/assets/flows/`, butuh Python dan Pillow). Jalankan lagi setelah perubahan UI.

## Yang sudah diuji, dan yang belum
Berkas diuji di mesin pengembangan dengan domain `localhost` dan `auth.localhost`: kedua image terbangun, stack start dalam kondisi sehat, Caddy menyajikan aplikasi web (deep link, cache panjang untuk berkas ber-hash, tanpa cache untuk service worker) dan meneruskan api, login demo lewat Keycloak asli berjalan dari ujung ke ujung, setiap halaman peran guru termuat, dashboard job dan konsol admin Keycloak tidak terjangkau, dan `X-Forwarded-For` palsu tidak memberi tamu jatah baru. Pengujian menemukan dua cacat yang sudah diperbaiki: api menolak alamat metadata HTTP biasa di jaringan internal (`Keycloak__RequireHttpsMetadata`), dan impor realm Keycloak gagal di SQL Server karena deskripsi client lebih dari 255 karakter.

**Belum diuji** (butuh hal yang tidak ada di mesin pengembangan): DNS sungguhan dan sertifikat Let's Encrypt, Cloudflare R2 (unggahan dari browser dan aturan CORS), server SMTP sungguhan, key Claude, dan server 4 GB di bawah beban. Pengaturan R2 dan email mengikuti konfigurasi aplikasi sendiri (`Storage:*`, `Email:*`), yang dicakup tes integrasi terhadap SeaweedFS dan pengirim palsu.

**Belum dibangun**: backup otomatis. Database ada di volume `sqlserver-data`; salin dengan `BACKUP DATABASE` lewat `sqlcmd` di container `sqlserver` sebelum Anda mengandalkan demo untuk sesuatu yang tidak bisa dibuat ulang (data demo hasil seed selalu bisa dibuat ulang).
