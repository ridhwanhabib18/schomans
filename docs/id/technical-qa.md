# Tanya jawab teknis SchoMans

Kumpulan pertanyaan dan alasan di balik keputusan teknis SchoMans, untuk referensi dan persiapan wawancara. Versi Inggris ada di tab Technical Q&A. Status lisensi dan produk berdasarkan pengetahuan sampai pertengahan 2026; cek ulang sumber resmi sebelum mengambil keputusan.

## 1. Apakah ada komponen tech stack yang berbayar?

Hampir semua komponen open source dan gratis. Yang benar-benar berbayar hanya Claude API (per token) dan hosting server.

| Kategori | Komponen | Catatan |
| --- | --- | --- |
| Berbayar | Claude API, VPS/Azure, domain (opsional), gateway WhatsApp (opsional) | Pasang batas budget dan mode mock untuk AI |
| Gratis bersyarat | SQL Server | Developer hanya untuk dev/test; Express boleh produksi dengan batas 10 GB per database |
| Gratis bersyarat | QuestPDF | Gratis untuk individu dan perusahaan dengan omzet < 1 juta USD/tahun |
| Gratis bersyarat | Seq, Hangfire, Docker Desktop, GitHub Actions, Cloudflare R2 | Lisensi individu, versi core, pemakaian pribadi, repo publik, dan free tier sudah cukup untuk portofolio |
| Gratis | Semua komponen lain | MIT, Apache 2.0, atau BSD |

## 2. State management React apa yang terbaik?

Tidak ada satu yang terbaik; yang penting adalah memisahkan dua jenis state. Server state (data dari API) dipegang **TanStack Query**, yang mengurus caching, loading, refetch, dan invalidasi. Client state (sesi login, wizard formulir, preferensi UI) dipegang state manager kecil.

## 3. Zustand atau MobX?

Dipilih **Zustand**. Karena porsi terbesar state sudah ditangani TanStack Query, client state SchoMans kecil, dan Zustand paling ringkas untuk itu.

| Aspek | Zustand | MobX |
| --- | --- | --- |
| Gaya | Fungsional, store berupa hook | OOP, class dengan property observable |
| Boilerplate | Sangat sedikit | Sedikit, perlu `makeAutoObservable` dan `observer` |
| Kurva belajar | Paling mudah | Perlu memahami observable, action, reaction |
| Ukuran bundle | Sangat kecil | Lebih besar |
| Popularitas di lowongan | Lebih sering untuk proyek React baru | Lebih sering di proyek enterprise lama |

Kombinasi TanStack Query + Zustand juga pola paling umum di proyek React saat ini, sehingga mudah dikenali rekruter.

## 4. Kenapa MinIO diganti?

Bukan karena berbayar, tapi karena edisi gratisnya kurang meyakinkan untuk proyek baru. Sepanjang 2025 web console edisi komunitas dipangkas, MinIO berhenti merilis binary dan image Docker siap pakai, dan edisi komunitas masuk mode maintenance. Lisensinya juga AGPL v3, yang sering dihindari perusahaan.

## 5. Bagaimana tetap mendapat pengalaman S3?

Backend memakai AWSSDK.S3 dengan satu implementasi `S3FileStorage` di balik interface `IFileStorage`. Di lokal memakai SeaweedFS di Docker (Apache 2.0), di demo live memakai Cloudflare R2 (free tier; biasanya perlu menambahkan metode pembayaran). Praktik yang dipakai sama dengan produksi AWS S3: bucket privat dan publik terpisah, presigned URL, struktur key, dan lifecycle rule.

## 6. Apakah Keycloak perlu diganti?

Tidak; Keycloak dipertahankan.

| Opsi | Kelebihan | Kekurangan |
| --- | --- | --- |
| Keycloak (dipilih) | Standar enterprise di Indonesia, pengalaman OIDC/SSO nyata, fitur auth sudah jadi | Boros RAM (\~1 GB), konfigurasi realm dan reverse proxy sulit |
| ASP.NET Core Identity | Bawaan .NET, tanpa container tambahan, mudah di-debug | Halaman auth dibuat sendiri, tidak ada SSO |
| Identity + OpenIddict | Server OIDC native .NET | Setup paling rumit |
| Duende IdentityServer | Matang | Berbayar untuk komersial |
| Auth0, Clerk, Entra External ID | Setup cepat | Vendor lock-in, berbayar di atas kuota |

Alasan: nilai di CV untuk perusahaan enterprise dan perbankan, dan beban setupnya hanya di awal. Mitigasinya: ekspor realm ke JSON, atur hostname dan proxy sejak awal, dan deploy sejak Fase 1.

## 7. Apa beda mediator dan MediatR?

**Mediator** adalah design pattern: pengirim tidak memanggil penerima secara langsung, tapi lewat perantara. **MediatR** adalah library yang mengimplementasikan pattern itu (berlisensi komersial sejak 2025). **Mediator** (martinothamar) adalah library lain dengan nama sama, berlisensi MIT.

```csharp
// With the mediator pattern
app.MapPost("/attendance/sessions", (OpenSessionCommand command, IMediator mediator)
    => mediator.Send(command));   // endpoint does not know the handler

// Without a mediator (SchoMans)
app.MapPost("/attendance/sessions", (OpenSessionCommand command, OpenSessionHandler handler)
    => handler.Handle(command));  // endpoint calls the handler directly
```

## 8. Apakah dari awal SchoMans tidak memakai MediatR?

Benar, MediatR tidak pernah disarankan. Namun saran awal masih menyebut library Mediator sebagai alternatif dan memuat folder `Behaviors/`, yang merupakan konsep pipeline mediator. Keduanya sudah dihapus, sehingga sekarang SchoMans tidak memakai pattern mediator sama sekali.

## 9. Untuk portofolio, lebih bagus pakai mediator atau tidak?

Tanpa mediator, dengan alasan tertulis di ADR-001. Yang membuat portofolio berkesan bukan pilihannya, tapi kemampuan menjelaskannya. Jawaban seperti "saya tahu MediatR, tapi untuk proyek ini saya memilih memanggil handler langsung karena alasan A dan B" biasanya lebih berkesan bagi interviewer senior daripada sekadar mengikuti template. Jika target kerja jelas memakai MediatR, library Mediator (MIT) bisa dipertimbangkan karena kodenya hampir identik.

## 10. Kenapa Minimal API, bukan Controller?

Minimal API selaras dengan susunan per fitur dan pemanggilan handler langsung; controller hanya akan menjadi lapisan penerus. Dependency di-inject per endpoint, endpoint filter cocok untuk validasi tanpa mediator, dan OpenAPI bawaan dipakai untuk generate client orval. Detailnya ada di ADR-002.

## 11. Apa bedanya Minimal API dengan Controller?

| Aspek | Controller | Minimal API |
| --- | --- | --- |
| Bentuk | Class turunan `ControllerBase` | Fungsi yang dipetakan ke route |
| Konfigurasi | Attribute: `[Route]`, `[HttpPost]`, `[Authorize]` | Method berantai: `MapPost`, `MapGroup`, `RequireAuthorization` |
| Dependency injection | Constructor, untuk semua action | Per endpoint |
| Filter | Action filter MVC | Endpoint filter |
| Validasi otomatis | `[ApiController]` + DataAnnotations | Dukungan bawaan sejak .NET 10; SchoMans memakai FluentValidation lewat endpoint filter |
| Fitur MVC | Lengkap | Lebih ramping |
| Performa | Sedikit lebih berat | Lebih ringan, mendukung Native AOT |
| Pengelompokan | Otomatis per class | Harus disiplin sendiri |

Keduanya berjalan di atas ASP.NET Core yang sama: middleware, autentikasi, DI, dan routing.

## 12. Apa kekurangan Minimal API dan cara mengatasinya?

| Kekurangan | Cara mengatasi (konvensi di PRD) |
| --- | --- |
| Rawan berantakan | Satu file per fitur + `MapGroup`, tidak ada endpoint di `Program.cs` |
| Lambda sulit dibaca dan di-debug | Method static bernama, endpoint tanpa logika bisnis |
| Binding parameter kadang mengejutkan | Sumber parameter eksplisit, `[AsParameters]` untuk query |
| Upload `IFormFile` terkena antiforgery | `.DisableAntiforgery()` karena API memakai JWT |
| Fitur MVC lebih sedikit | Library wajib mendukung Minimal API |
| Metadata endpoint panjang | Konfigurasi di level group, extension method seperti `.WithValidation<T>()` |
| Unit test endpoint sulit | Unit test di handler, integration test per endpoint |
| Familiaritas tim | Alasan tertulis di ADR-002 |

## 13. Bagian mana yang paling berat dikerjakan?

| Peringkat | Bagian | Kenapa berat | Cara meringankan |
| --- | --- | --- | --- |
| 1 | Fondasi auth Keycloak + .NET + React | Banyak konfigurasi harus pas, error sering samar | Kerjakan paling awal, ekspor realm ke JSON, test akses per peran |
| 2 | Penilaian & rapor | Aturan domain Kurikulum Merdeka dan tata letak PDF | Putuskan jenjang dan format rapor dulu, pakai contoh rapor asli |
| 3 | Absensi QR realtime | QR berganti, SignalR, kamera di browser HP, scan bersamaan | Library scanner teruji, tes di HP sungguhan, unique constraint sesi + siswa |
| 4 | PPDB & approval | Pada dasarnya state machine dengan banyak status | Gambar diagram status dulu, transisi di method entitas |
| 5 | Kualitas fitur AI | Iterasi prompt dan set uji | Bangun set uji sebelum menulis prompt |

Yang sering diremehkan: deployment (SQL Server dan Keycloak boros RAM; deploy sejak Fase 1) dan seed data demo yang realistis.

## 14. Kenapa tutor AI untuk siswa (AI-07) dihapus?

Fitur ini paling berat dipastikan benar: tidak memberi jawaban langsung, menolak topik di luar pelajaran, dan menangani kasus sensitif pada pengguna anak-anak. Butuh guardrail ketat dan pengujian terus-menerus, sehingga dikeluarkan dari scope dan dicatat di non-tujuan.

## 15. Fitur AI apa saja yang dipakai?

Delapan fitur: ringkasan perkembangan untuk orang tua (AI-01, wajib), deskripsi capaian rapor (AI-02), ekstraksi berkas PPDB (AI-03), asisten kepala sekolah berbasis tool use (AI-04), umpan balik esai berbasis rubrik (AI-05), generator soal (AI-06), chatbot info sekolah (AI-08), dan draf pengumuman dan surat (AI-09). Semuanya mengikuti prinsip yang sama: data seminimal mungkin, hasil berupa draf yang ditinjau manusia, output JSON tervalidasi, batas budget, dan mode mock.

## 16. Kenapa kode dan aplikasi full bahasa Inggris, tapi dokumentasi dua bahasa?

Kode bahasa Inggris adalah standar industri dan konsisten dengan nama framework. Aplikasi bahasa Inggris membuat demo bisa dicoba rekruter dari mana saja, dan struktur i18n memungkinkan bahasa Indonesia ditambahkan nanti tanpa mengubah kode. Dokumentasi dua bahasa melayani dua pembaca: versi Inggris untuk reviewer internasional, versi Indonesia untuk pembaca lokal dan untuk persiapan wawancara.
