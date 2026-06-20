using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
namespace per10SatisWPF
{
public class CanliUrunModel
    {
        public int    UrunID    { get; set; }
        public string UrunAdi  { get; set; }
        public int    MevcutStok { get; set; }
    }
    public partial class AyarlarWindow : Window
    {
        private readonly string _connStr = ConfigurationManager.ConnectionStrings["Per10DB"].ConnectionString;
        private int _seciliUrunID = -1;
        private bool _raporYetkisiVar = false;
        private int _hedefTabIndeks = 0;
        private int _sonSeciliTab = 0;
        private List<CanliUrunModel> _canliKritikUrunler = new List<CanliUrunModel>();
        private List<CanliUrunModel> _canliHareketsizUrunler = new List<CanliUrunModel>();
        private List<(int TurID, string Ikon, string Ad)> _kategoriler = new();
        private string _aktifListeTipi = "";
        private readonly Dictionary<string, (string Yanit, DateTime Zaman)> _asistanCache = new();
        private int    _kampanyaUrunID   = -1;
        private decimal _kampanyaEskiFiyat = 0;

        public AyarlarWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)

        {
            VeritabanindanCanliVerileriCek();
            txtYeniAdminID.Text = Properties.Settings.Default.KullaniciAdi;
            txtYeniAdminSifre.Text = Properties.Settings.Default.Sifre;
            txtYeniRaporPin.Text = Properties.Settings.Default.RaporSifresi;
            // Tarih varsayılanları
            dpBaslangic.SelectedDate = DateTime.Today;
            dpBitis.SelectedDate = DateTime.Today;
            dpGrafBaslangic.SelectedDate = DateTime.Today;
            dpGrafBitis.SelectedDate = DateTime.Today;
            dpYikamaBaslangic.SelectedDate = DateTime.Today;
            dpYikamaBitis.SelectedDate = DateTime.Today;

            // ComboBox doldur
            KategorileriDBdenYukle();
            cmbKategoriFiltre.SelectedIndex = 0;
            cmbYeniTur.SelectedIndex = 0;
            txtTelegramChatId.Text = Properties.Settings.Default.TelegramChatId;
            MarkalariYukle();
            txtTelegramToken.Text = Properties.Settings.Default.TelegramToken;
        }

        private void btnTelegramKaydet_Click(object sender, RoutedEventArgs e)
        {
            // Giriş kutularının boş olup olmadığını kontrol ediyoruz
            if (string.IsNullOrWhiteSpace(txtTelegramChatId.Text) || string.IsNullOrWhiteSpace(txtTelegramToken.Text))
            {
                MessageBox.Show("Lütfen hem Chat ID hem de Bot Token alanlarını doldurun!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Kullanıcının TextBox'lara yazdığı canlı değerleri hafızaya kilitliyoruz
            Properties.Settings.Default.TelegramChatId = txtTelegramChatId.Text.Trim();
            Properties.Settings.Default.TelegramToken = txtTelegramToken.Text.Trim();
            Properties.Settings.Default.Save(); // Bilgisayar kapansa bile unutulmaması için kaydet!

            MessageBox.Show("✅ Telegram bildirim ayarları başarıyla güncellendi!\nArtık raporlar bu dükkanın kendi botu üzerinden gönderilecektir.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ─── ÜRÜN YÖNETİMİ ────────────────────────────────────────────
        private void cmbKategoriFiltre_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Artık ComboBoxItem değil, doğrudan SelectedValue üzerinden TurID alıyoruz
            if (cmbKategoriFiltre.SelectedValue != null && int.TryParse(cmbKategoriFiltre.SelectedValue.ToString(), out int turID))
            {
                UrunleriListele(turID);
            }
        }

        private void UrunleriListele(int turID)
        {
            var tablo = new DataTable();
            try
            {
                using var conn = new SqlConnection(_connStr);
                string q = @"SELECT u.UrunID, u.UrunAdi, m.MarkaAdi,
                                    u.AlisFiyati, u.SatisFiyati, u.MevcutStok, u.Barkod
                             FROM Urunler u
                             JOIN Markalar m ON u.MarkaID = m.MarkaID
                             WHERE u.TurID = @turID
                             ORDER BY m.MarkaAdi, u.UrunAdi";
                var cmd75 = new SqlCommand(q, conn);
                cmd75.Parameters.AddWithValue("@turID", turID);
                new SqlDataAdapter(cmd75).Fill(tablo);
            }
            catch (Exception ex) { MessageBox.Show($"Hata: {ex.Message}"); return; }

            var liste = tablo.AsEnumerable().Select(r => new
            {
                UrunID = r.Field<int>("UrunID"),
                TamAdi = $"{r["MarkaAdi"]} {r["UrunAdi"]}",
                AlisFiyatiText = $"{r.Field<decimal>("AlisFiyati"):N2} ₺",
                FiyatText = $"{r.Field<decimal>("SatisFiyati"):N2} ₺",
                MevcutStok = r.Field<int>("MevcutStok"),
                Barkod = r["Barkod"]?.ToString() ?? ""
            }).ToList();

            dgUrunler.ItemsSource = liste;
        }

        private void dgUrunler_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgUrunler.SelectedItem == null) return;

            dynamic secili = dgUrunler.SelectedItem;
            _seciliUrunID = secili.UrunID;

            // Formu doldur
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand(
                    "SELECT UrunAdi, AlisFiyati, SatisFiyati, MevcutStok, Barkod FROM Urunler WHERE UrunID=@id", conn);
                cmd.Parameters.AddWithValue("@id", _seciliUrunID);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    txtGuncelUrunAdi.Text = r["UrunAdi"].ToString();
                    txtAlisFiyati.Text = r["AlisFiyati"].ToString();
                    txtSatisFiyati.Text = r["SatisFiyati"].ToString();
                    txtStok.Text = r["MevcutStok"].ToString();
                    txtBarkodGuncelle.Text = r["Barkod"]?.ToString() ?? "";
                }
            }
            catch { }
        }

        private void btnGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (_seciliUrunID < 0) { MessageBox.Show("Listeden bir ürün seçin.", "Uyarı"); return; }
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand("sp_UrunGuncelleDetayli", conn)
                { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@UrunID", _seciliUrunID);
                cmd.Parameters.AddWithValue("@AlisFiyati", Convert.ToDecimal(txtAlisFiyati.Text));
                cmd.Parameters.AddWithValue("@SatisFiyati", Convert.ToDecimal(txtSatisFiyati.Text));
                cmd.Parameters.AddWithValue("@Stok", Convert.ToInt32(txtStok.Text));
                cmd.ExecuteNonQuery();

                // Ürün adı güncelle
                if (!string.IsNullOrWhiteSpace(txtGuncelUrunAdi.Text))
                {
                    using var cmdAd = new SqlCommand("UPDATE Urunler SET UrunAdi=@ad WHERE UrunID=@id", conn);
                    cmdAd.Parameters.AddWithValue("@ad", txtGuncelUrunAdi.Text.Trim());
                    cmdAd.Parameters.AddWithValue("@id", _seciliUrunID);
                    cmdAd.ExecuteNonQuery();
                }

                // Barkod güncelle
                if (!string.IsNullOrWhiteSpace(txtBarkodGuncelle.Text))
                {
                    using var cmdB = new SqlCommand("UPDATE Urunler SET Barkod=@b WHERE UrunID=@id", conn);
                    cmdB.Parameters.AddWithValue("@b", txtBarkodGuncelle.Text.Trim());
                    cmdB.Parameters.AddWithValue("@id", _seciliUrunID);
                    cmdB.ExecuteNonQuery();
                }

                GosterMesaj(bdGuncelMesaj, lblGuncelMesaj, "✅ Ürün başarıyla güncellendi!");
                if (cmbKategoriFiltre.SelectedValue != null && int.TryParse(cmbKategoriFiltre.SelectedValue.ToString(), out int turID))
                    UrunleriListele(turID);
            }
            catch (Exception ex) { MessageBox.Show($"Güncelleme hatası: {ex.Message}"); }
        }

        // ─── YENİ ÜRÜN ────────────────────────────────────────────────
        private void MarkalariYukle()
        {
            try
            {
                using var conn = new SqlConnection(_connStr);
                var da = new SqlDataAdapter("SELECT MarkaID, MarkaAdi FROM Markalar ORDER BY MarkaAdi", conn);
                var dt = new DataTable();
                da.Fill(dt);
                cmbYeniMarka.DisplayMemberPath = "MarkaAdi";
                cmbYeniMarka.SelectedValuePath = "MarkaID";
                cmbYeniMarka.ItemsSource = dt.DefaultView;
                cmbMarkaSil.DisplayMemberPath = "MarkaAdi";
                cmbMarkaSil.SelectedValuePath = "MarkaID";
                cmbMarkaSil.ItemsSource = dt.DefaultView;
            }
            catch { }
        }

        private void btnYeniUrunEkle_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtYeniUrunAdi.Text) ||
                string.IsNullOrWhiteSpace(txtYeniAlis.Text) ||
                string.IsNullOrWhiteSpace(txtYeniSatis.Text) ||
                string.IsNullOrWhiteSpace(txtYeniStok.Text))
            {
                MessageBox.Show("Lütfen tüm zorunlu alanları doldurun.", "Eksik Bilgi");
                return;
            }

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand("sp_AkilliUrunEkle", conn)
                { CommandType = CommandType.StoredProcedure };

                cmd.Parameters.AddWithValue("@YeniMarkaAdi", DBNull.Value);
                cmd.Parameters.AddWithValue("@MevcutMarkaID", cmbYeniMarka.SelectedValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TurID", cmbYeniTur.SelectedValue ?? 1);
                cmd.Parameters.AddWithValue("@UrunAdi", txtYeniUrunAdi.Text.Trim());
                cmd.Parameters.AddWithValue("@AlisFiyati", Convert.ToDecimal(txtYeniAlis.Text));
                cmd.Parameters.AddWithValue("@SatisFiyati", Convert.ToDecimal(txtYeniSatis.Text));
                cmd.Parameters.AddWithValue("@Stok", Convert.ToInt32(txtYeniStok.Text));
                cmd.Parameters.AddWithValue("@KritikStok", Convert.ToInt32(txtYeniKritikStok.Text));
                cmd.ExecuteNonQuery();

                // Barkod ekle — yeni eklenen ürünün ID'sini al
                if (!string.IsNullOrWhiteSpace(txtYeniBarkod.Text))
                {
                    using var cmdID = new SqlCommand("SELECT CAST(IDENT_CURRENT('Urunler') AS INT)", conn);
                    int yeniUrunID = Convert.ToInt32(cmdID.ExecuteScalar());
                    using var cmdB = new SqlCommand(
                        "UPDATE Urunler SET Barkod=@b WHERE UrunID=@id", conn);
                    cmdB.Parameters.AddWithValue("@b", txtYeniBarkod.Text.Trim());
                    cmdB.Parameters.AddWithValue("@id", yeniUrunID);
                    cmdB.ExecuteNonQuery();
                }

                GosterMesaj(bdEkleMesaj, lblEkleMesaj,
                    $"✅ '{txtYeniUrunAdi.Text}' başarıyla eklendi!");

                txtYeniUrunAdi.Clear();
                txtYeniAlis.Clear();
                txtYeniSatis.Clear();
                txtYeniStok.Clear();
                txtYeniBarkod.Clear();
            }
            catch (Exception ex) { MessageBox.Show($"Kayıt hatası: {ex.Message}"); }
        }

        // ─── SATIŞ RAPORU ─────────────────────────────────────────────
        private void btnRaporGetir_Click(object sender, RoutedEventArgs e)
        {
            if (dpBaslangic.SelectedDate == null || dpBitis.SelectedDate == null) return;

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();

                DateTime bas = dpBaslangic.SelectedDate.Value.Date.AddHours(8);
                DateTime bitis = dpBitis.SelectedDate.Value.Date.AddDays(1).AddHours(3);

                decimal ciro = 0, kar = 0, nakit = 0, kredi = 0;

                // 1. ÖZET HESAPLAMALARI: SQL'den salt veriyi çekip C# ile parçalıyoruz
                using (var cmd = new SqlCommand(@"
            SELECT 
                sp.Indirim, MAX(s.OdemeYontemi) AS OdemeYontemi,
                SUM(s.birimsatisfiyati * s.Miktar) AS SepetCiro,
                SUM((s.birimsatisfiyati - s.birimalisfiyati) * s.Miktar) AS SepetKar
            FROM Sepetler sp
            JOIN Satislar s ON sp.SepetID = s.SepetID
            WHERE sp.Tarih BETWEEN @bas AND @bitis
            GROUP BY sp.SepetID, sp.Indirim", conn))
                {
                    cmd.Parameters.AddWithValue("@bas", bas);
                    cmd.Parameters.AddWithValue("@bitis", bitis);
                    using var r = cmd.ExecuteReader();

                    while (r.Read())
                    {
                        string yontem = r["OdemeYontemi"]?.ToString() ?? "";

                        // Hediye ise ciro ve kâr hesabına katmıyoruz (senin mantığınla aynı)
                        if (yontem == "Hediye") continue;

                        decimal sepetCiro = Convert.ToDecimal(r["SepetCiro"]) - Convert.ToDecimal(r["Indirim"]);
                        decimal sepetKar = Convert.ToDecimal(r["SepetKar"]) - Convert.ToDecimal(r["Indirim"]);

                        ciro += sepetCiro;
                        kar += sepetKar;

                        // Nakit, Kredi ve Parçalı (200 N 300 K) Ayrıştırması
                        if (yontem == "Nakit")
                        {
                            nakit += sepetCiro;
                        }
                        else if (yontem == "Kredi Kartı")
                        {
                            kredi += sepetCiro;
                        }
                        else if (yontem.Contains("N") || yontem.Contains("K"))
                        {
                            nakit += ParcaliDegerCek(yontem, "N");
                            kredi += ParcaliDegerCek(yontem, "K");
                        }
                    }
                }

                lblCiro.Text = $"{ciro:N2} ₺";
                lblKar.Text = $"{kar:N2} ₺";
                lblSatisNakit.Text = $"{nakit:N2} ₺";
                lblSatisKredi.Text = $"{kredi:N2} ₺";
                lblKar.Foreground = kar >= 0 ? (Brush)FindResource("AccentGreen") : (Brush)FindResource("AccentRed");

                // 2. DATAGRID TABLOSU (Senin kodun tamamen aynı bırakıldı)
                var dt = new DataTable();
                using (var cmd2 = new SqlCommand(@"
            SELECT sp.SepetID, sp.Indirim, sp.Tarih,
                   SUM(s.birimsatisfiyati * s.Miktar) - sp.Indirim AS NetTutar,
                   MAX(s.OdemeYontemi) AS OdemeYontemi
            FROM Sepetler sp
            JOIN Satislar s ON s.SepetID = sp.SepetID
            WHERE sp.Tarih BETWEEN @bas AND @bitis
            GROUP BY sp.SepetID, sp.Indirim, sp.Tarih
            ORDER BY sp.Tarih DESC", conn))
                {
                    cmd2.Parameters.AddWithValue("@bas", bas);
                    cmd2.Parameters.AddWithValue("@bitis", bitis);
                    new SqlDataAdapter(cmd2).Fill(dt);
                }

                var liste = dt.AsEnumerable().Select(r => new
                {
                    SepetID = r.Field<int>("SepetID"),
                    Toplam = $"{r.Field<decimal>("NetTutar"):N2} ₺",
                    Indirim = r.Field<decimal>("Indirim") > 0 ? $"-{r.Field<decimal>("Indirim"):N2} ₺" : "—",
                    Odeme = r["OdemeYontemi"]?.ToString() ?? "Belirtilmedi",
                    Tarih = r.Field<DateTime>("Tarih").ToString("dd.MM.yyyy HH:mm")
                }).ToList();

                dgSepetler.ItemsSource = liste;
                lblIslem.Text = $"{liste.Count} işlem";
            }
            catch (Exception ex) { MessageBox.Show($"Rapor hatası: {ex.Message}"); }
        }
        private decimal ParcaliDegerCek(string metin, string anahtar)
        {
            try
            {
                if (!metin.Contains(anahtar)) return 0;

                string[] parcalar = metin.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parcalar.Length; i++)
                {
                    if (parcalar[i] == anahtar && i > 0)
                    {
                        if (decimal.TryParse(parcalar[i - 1], out decimal deger))
                            return deger;
                    }
                }
            }
            catch { }
            return 0;
        }
        private void btnSepetIptal_Click(object sender, RoutedEventArgs e)
        {
            // 1. Listeden sepet seçilmiş mi kontrol et
            if (dgSepetler.SelectedItem == null)
            {
                MessageBox.Show("Lütfen iptal etmek istediğiniz sepeti listeden seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            dynamic seciliSepet = dgSepetler.SelectedItem;
            int iptalEdilecekID = seciliSepet.SepetID;

            // 2. Yanlışlıkla silmelere karşı teyit al
            var onay = MessageBox.Show(
                $"Seçili {iptalEdilecekID} numaralı sepeti iptal etmek istediğinize emin misiniz?\n\nBu işlem satılan ürünlerin stoklarını otomatik olarak geri yükleyecektir.",
                "Sepet İptal Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (onay == MessageBoxResult.Yes)
            {
                try
                {
                    using var conn = new SqlConnection(_connStr);
                    conn.Open();

                    // 3. Senin veritabanında zaten var olan iptal prosedürünü çalıştır
                    using var sp = new SqlCommand("sp_SepetIptal", conn) { CommandType = CommandType.StoredProcedure };
                    sp.Parameters.AddWithValue("@SepetID", iptalEdilecekID);
                    sp.ExecuteNonQuery();

                    MessageBox.Show($"✅ {iptalEdilecekID} numaralı sepet iptal edildi ve stoklar geri yüklendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 4. İşlem bitince listeyi ve özet panelini tazele
                    btnRaporGetir_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"İptal işlemi sırasında hata oluştu: {ex.Message}", "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        // ─── ÜRÜN VE MARKA SİLME ──────────────────────────────────────────────
        private void btnUrunSil_Click(object sender, RoutedEventArgs e)
        {
            if (_seciliUrunID < 0) { MessageBox.Show("Önce listeden silinecek ürünü seçin.", "Uyarı"); return; }

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();

                using var checkCmd = new SqlCommand("SELECT COUNT(DISTINCT SepetID) FROM Satislar WHERE UrunID = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", _seciliUrunID);
                int sepetSayisi = (int)checkCmd.ExecuteScalar();

                if (sepetSayisi > 0)
                {
                    var onay = MessageBox.Show($"Bu ürüne ait {sepetSayisi} adet sepet kaydı bulundu!\n\nÜrünü silebilmek için bu sepetler Excel(CSV) olarak dışa aktarılacak ve ardından veritabanından kalıcı olarak silinecektir.\n\nOnaylıyor musunuz?", "Satış Kayıtları Var", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (onay != MessageBoxResult.Yes) return;

                    if (!SepetleriDisaAktarVeSil(conn, $"UrunID = {_seciliUrunID}")) return;
                }
                using var cmdDelStok = new SqlCommand("DELETE FROM StokHareketleri WHERE UrunID = @id", conn);
                cmdDelStok.Parameters.AddWithValue("@id", _seciliUrunID);
                cmdDelStok.ExecuteNonQuery();

                using var cmdDel = new SqlCommand("DELETE FROM Urunler WHERE UrunID = @id", conn);
                cmdDel.Parameters.AddWithValue("@id", _seciliUrunID);
                cmdDel.ExecuteNonQuery();

                MessageBox.Show("✅ Ürün başarıyla silindi!", "Sistem");
                _seciliUrunID = -1;
                if (cmbKategoriFiltre.SelectedValue != null && int.TryParse(cmbKategoriFiltre.SelectedValue.ToString(), out int turID))
                    UrunleriListele(turID);
            }
            catch (Exception ex) { MessageBox.Show($"Hata: {ex.Message}"); }
        }

        private void btnMarkaSil_Click(object sender, RoutedEventArgs e)
        {
            if (cmbMarkaSil.SelectedValue == null) { MessageBox.Show("Lütfen silinecek markayı seçin.", "Uyarı"); return; }

            int markaID = (int)cmbMarkaSil.SelectedValue;
            string markaAdi = cmbMarkaSil.Text;

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();

                using var checkCmd = new SqlCommand("SELECT COUNT(DISTINCT s.SepetID) FROM Satislar s JOIN Urunler u ON s.UrunID = u.UrunID WHERE u.MarkaID = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", markaID);
                int sepetSayisi = (int)checkCmd.ExecuteScalar();

                if (sepetSayisi > 0)
                {
                    var onay = MessageBox.Show($"'{markaAdi}' markasına ait ürünlerin geçtiği {sepetSayisi} adet sepet kaydı bulundu!\n\nMarkayı silebilmek için bu sepetler Excel(CSV) olarak dışa aktarılacak ve veritabanından kalıcı olarak silinecektir.\n\nOnaylıyor musunuz?", "Satış Kayıtları Var", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (onay != MessageBoxResult.Yes) return;

                    if (!SepetleriDisaAktarVeSil(conn, $"UrunID IN (SELECT UrunID FROM Urunler WHERE MarkaID = {markaID})")) return;
                }

                new SqlCommand($"DELETE FROM StokHareketleri WHERE UrunID IN (SELECT UrunID FROM Urunler WHERE MarkaID = {markaID})", conn).ExecuteNonQuery();
                new SqlCommand($"DELETE FROM Urunler WHERE MarkaID = {markaID}", conn).ExecuteNonQuery();
                new SqlCommand($"DELETE FROM Markalar WHERE MarkaID = {markaID}", conn).ExecuteNonQuery();

                MessageBox.Show($"✅ '{markaAdi}' markası ve ona bağlı tüm ürünler silindi!", "Sistem");
                MarkalariYukle();
                if (cmbKategoriFiltre.SelectedValue != null && int.TryParse(cmbKategoriFiltre.SelectedValue.ToString(), out int turID))
                    UrunleriListele(turID);
            }
            catch (Exception ex) { MessageBox.Show($"Hata: {ex.Message}"); }
        }

        // Ortak Yedekleme ve Silme Metodu
        private bool SepetleriDisaAktarVeSil(SqlConnection conn, string sqlSart)
        {
            // Tarih aralığını bul (Dosya adı için)
            using var dateCmd = new SqlCommand($"SELECT MIN(sp.Tarih), MAX(sp.Tarih) FROM Sepetler sp JOIN Satislar s ON sp.SepetID = s.SepetID WHERE s.{sqlSart}", conn);
            using var reader = dateCmd.ExecuteReader();
            DateTime minDate = DateTime.Today, maxDate = DateTime.Today;
            if (reader.Read() && reader[0] != DBNull.Value)
            {
                minDate = Convert.ToDateTime(reader[0]);
                maxDate = Convert.ToDateTime(reader[1]);
            }
            reader.Close();

            string dosyaAdi = $"{minDate:dd.MM.yyyy}-{maxDate:dd.MM.yyyy}_SatisLog.csv";
            var dlg = new Microsoft.Win32.SaveFileDialog { FileName = dosyaAdi, DefaultExt = ".csv", Filter = "CSV Dosyası|*.csv" };
            if (dlg.ShowDialog() != true) return false;

            // Sadece hedef sepetleri çek
            var dt = new DataTable();
            string exportQuery = $@"
        SELECT sp.SepetID, SUM(s.birimsatisfiyati * s.Miktar) - sp.Indirim AS NetTutar, sp.Indirim, sp.Tarih
        FROM Sepetler sp
        JOIN Satislar s ON s.SepetID = sp.SepetID
        WHERE sp.SepetID IN (SELECT DISTINCT SepetID FROM Satislar WHERE {sqlSart})
        GROUP BY sp.SepetID, sp.Indirim, sp.Tarih
        ORDER BY sp.Tarih";

            using (var exportCmd = new SqlCommand(exportQuery, conn))
                new SqlDataAdapter(exportCmd).Fill(dt);

            // CSV oluştur
            var satirlar = new List<string> { "Sepet #;Net Tutar;Indirim;Tarih" };
            foreach (DataRow row in dt.Rows)
                satirlar.Add($"{row["SepetID"]};{Convert.ToDecimal(row["NetTutar"]):N2} ₺;{Convert.ToDecimal(row["Indirim"]):N2} ₺;{Convert.ToDateTime(row["Tarih"]):dd.MM.yyyy HH:mm}");

            System.IO.File.WriteAllLines(dlg.FileName, satirlar, System.Text.Encoding.UTF8);

            // BÜYÜK TEMİZLİK (Sadece o sepetleri uçur)
            // SepetleriDisaAktarVeSil metodundaki temizlik kısmı:
            new SqlCommand(
                "DISABLE TRIGGER trg_SatisİptalStokGuncelle ON Satislar; " +
                $"DELETE FROM Satislar WHERE SepetID IN (SELECT DISTINCT SepetID FROM Satislar WHERE {sqlSart}); " +
                "DELETE FROM Sepetler WHERE SepetID NOT IN (SELECT DISTINCT SepetID FROM Satislar); " +
                "ENABLE TRIGGER trg_SatisİptalStokGuncelle ON Satislar;", conn).ExecuteNonQuery();
            return true;
        }
        private void dgSepetler_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgSepetler.SelectedItem == null) return;
            dynamic secili = dgSepetler.SelectedItem;

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand("sp_SepetDetayiniGetir", conn)
                { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@SepetID", secili.SepetID);
                var dt = new DataTable();
                new SqlDataAdapter(cmd).Fill(dt);

                dgSepetDetay.ItemsSource = dt.AsEnumerable().Select(r => new
                {
                    UrunAdi = $"{r["MarkaAdi"]} {r["UrunAdi"]}",
                    Miktar = r["Miktar"].ToString(),
                    Fiyat = $"{Convert.ToDecimal(r["birimsatisfiyati"]):N2} ₺"
                }).ToList();
            }
            catch { }
        }

        // ─── CSV EXPORT ───────────────────────────────────────────────
        private async void btnSatisExport_Click(object sender, RoutedEventArgs e)
        {
            if (dpBaslangic.SelectedDate == null || dpBitis.SelectedDate == null) return;
            if (dgSepetler.ItemsSource == null)
            { MessageBox.Show("Önce raporu getirin.", "Uyarı"); return; }

            var onay = MessageBox.Show(
                $"⚠️  DİKKAT!\n\n" +
                $"{dpBaslangic.SelectedDate.Value:dd.MM.yyyy} - {dpBitis.SelectedDate.Value:dd.MM.yyyy} tarihleri arasındaki\n" +
                $"tüm satış kayıtları CSV dosyasına aktarılacak ve ardından\n" +
                $"veritabanından KALICI OLARAK SİLİNECEKTİR.\n\n" +
                $"Bu işlem geri alınamaz. Devam etmek istiyor musunuz?",
                "Aktar ve Sil — Onay",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (onay != MessageBoxResult.Yes) return;

            string dosyaAdi = $"{dpBaslangic.SelectedDate.Value:dd.MM.yyyy} - {dpBitis.SelectedDate.Value:dd.MM.yyyy}_SatisLog.csv";
            var dlg = new Microsoft.Win32.SaveFileDialog
            { FileName = dosyaAdi, DefaultExt = ".csv", Filter = "CSV Dosyası|*.csv" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var satirlar = new System.Collections.Generic.List<string> { "Sepet #;Net Tutar;İndirim;Ödeme Yöntemi;Tarih" };

                foreach (dynamic item in dgSepetler.ItemsSource)
                    satirlar.Add($"{item.SepetID};{item.Toplam};{item.Indirim};{item.Odeme};{item.Tarih}");

                satirlar.Add("");
                satirlar.Add("--- OZET BİLGİLER ---;;;;");
                satirlar.Add($"Tarih Aralığı:;{dpBaslangic.SelectedDate.Value:dd.MM.yyyy} - {dpBitis.SelectedDate.Value:dd.MM.yyyy};;;");
                satirlar.Add($"Toplam Ciro:;{lblCiro.Text};;;");
                satirlar.Add($"Toplam Kar:;{lblKar.Text};;;");
                satirlar.Add($"Nakit Toplam:;{lblSatisNakit.Text};;;");
                satirlar.Add($"Kredi Toplam:;{lblSatisKredi.Text};;;");

                System.IO.File.WriteAllLines(dlg.FileName, satirlar, System.Text.Encoding.UTF8);

                // Veriler silinmeden önce mesai sonu Telegram bildirimini yeni şablonla gönder    
                await MesaiSonuTelegramGonder(
                    dpBaslangic.SelectedDate.Value,
                    dpBitis.SelectedDate.Value,
                    lblCiro.Text,
                    lblKar.Text,
                    lblSatisNakit.Text,
                    lblSatisKredi.Text,
                    lblIslem.Text
                );

                // SQL'den sil
                DateTime bas = dpBaslangic.SelectedDate.Value.Date.AddHours(8);
                DateTime bitis = dpBitis.SelectedDate.Value.Date.AddDays(1).AddHours(3);
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand(
                    "DISABLE TRIGGER trg_SatisIptalStokGuncelle ON Satislar; " +
                    "DELETE s FROM Satislar s JOIN Sepetler sp ON s.SepetID = sp.SepetID WHERE sp.Tarih BETWEEN @bas AND @bitis; " +
                    "DELETE FROM Sepetler WHERE Tarih BETWEEN @bas AND @bitis; " +
                    "ENABLE TRIGGER trg_SatisIptalStokGuncelle ON Satislar;", conn);
                cmd.Parameters.AddWithValue("@bas", bas);
                cmd.Parameters.AddWithValue("@bitis", bitis);
                cmd.ExecuteNonQuery();

                dgSepetler.ItemsSource = null;
                lblCiro.Text = "—"; lblKar.Text = "—"; lblIslem.Text = "—";
                MessageBox.Show($"✅ Kayıtlar aktarıldı, mesai sonu bildirim şablonu başarıyla gönderildi ve veriler silindi:\n{dlg.FileName}", "Başarılı");
            }
            catch (Exception ex) { MessageBox.Show($"Hata: {ex.Message}"); }
        }

        private async Task MesaiSonuTelegramGonder(DateTime basTarihi, DateTime bitisTarihi, string ciro, string kar, string nakit, string kredi, string islem)
        {
            string raporZamani = DateTime.Now.ToString("dd.MM.yyyy HH:mm", new System.Globalization.CultureInfo("tr-TR"));

            // SQL'den güncel stok durum sayılarını özet olarak çekiyoruz (Detaylı isim listesi yerine adet gösterimi)
            int toplamTukenenSayisi = 0;
            int toplamKritikSayisi = 0;
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                toplamTukenenSayisi = (int)new SqlCommand("SELECT COUNT(*) FROM Urunler WHERE MevcutStok = 0", conn).ExecuteScalar();
                toplamKritikSayisi = (int)new SqlCommand("SELECT COUNT(*) FROM Urunler WHERE MevcutStok > 0 AND MevcutStok <= 3", conn).ExecuteScalar();
            }
            catch { }

            // 🔥 TAMAMEN MAIN'DEKİ O İSTEDİĞİN 2. FORMATIN BİREBİR AYNISI YAPILDI:
            string mesaj = $"📊 *NW NEON OTO BAKIM — GÜN SONU MALI RAPORU*\n" +
                           $"━━━━━━━━━━━━━━━━━━━━━━\n" +
                           $"📅 *Kapanış:* {raporZamani}\n" +
                           $"━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                           $"🛒 *ÜRÜN SATIŞ DETAYLARI:*\n" +
                           $"💵 Nakit Satış: {nakit}\n" +
                           $"💳 Kartlı Satış: {kredi}\n" +
                           $"📦 *Toplam Ürün Cirosu:* {ciro}\n\n" +
                           $"🚿 *YIKAMA GELİR DETAYLARI:*\n" +
                           $"💵 Nakit Yıkama: 0,00 ₺\n" +
                           $"💳 Kartlı Yıkama: 0,00 ₺\n" +
                           $"🚿 *Toplam Yıkama Cirosu:* 0,00 ₺\n\n" +
                           $"━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                           $"💰 *KASA DAĞILIM ÖZETİ (Brüt):*\n" +
                           $"💵 Toplam Nakit Akışı: {nakit}\n" +
                           $"💳 Toplam Kart Akışı: {kredi}\n" +
                           $"📉 Toplam Yapılan İndirim: -0,00 ₺\n\n" +
                           $"🔥 *NET GÜNLÜK CİRO:* `{ciro}`\n\n" +
                           $"━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                           $"🚨 *DEPO & STOK GECELİK ÖZETİ:*\n" +
                           $"🛑 *Tamamen Biten Ürün:* {toplamTukenenSayisi} çeşit\n" +
                           $"⚠️ *Kritik Seviyedeki Ürün:* {toplamKritikSayisi} çeşit\n\n" +
                           $"_Not: Detaylı eksik listesine ve tedarik kalemlerine Akıllı Asistandan ulaşabilirsiniz._\n\n" +
                           $"━━━━━━━━━━━━━━━━━━━━━━\n" +
                           $"✨ _Başarılı bir mesai günü geride kaldı. İyi akşamlar dileriz!_ 👋";

            // Hatalı duran virgül silindi, doğrudan asenkron metod çağrılıyor
            await TelegramGonder(mesaj);
        }
        private void btnYikamaExport_Click(object sender, RoutedEventArgs e)
        {
            if (dpYikamaBaslangic.SelectedDate == null || dpYikamaBitis.SelectedDate == null) return;
            if (dgYikamalar.ItemsSource == null)
            { MessageBox.Show("Önce raporu getirin.", "Uyarı"); return; }

            var onay = MessageBox.Show(
                $"⚠️  DİKKAT!\n\n" +
                $"{dpYikamaBaslangic.SelectedDate.Value:dd.MM.yyyy} - {dpYikamaBitis.SelectedDate.Value:dd.MM.yyyy} tarihleri arasındaki\n" +
                $"tüm yıkama kayıtları CSV dosyasına aktarılacak ve ardından\n" +
                $"veritabanından KALICI OLARAK SİLİNECEKTİR.\n\n" +
                $"Bu işlem geri alınamaz. Devam etmek istiyor musunuz?",
                "Aktar ve Sil — Onay",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (onay != MessageBoxResult.Yes) return;

            string dosyaAdi = $"{dpYikamaBaslangic.SelectedDate.Value:dd.MM.yyyy} - {dpYikamaBitis.SelectedDate.Value:dd.MM.yyyy}_YikamaLog.csv";
            var dlg = new Microsoft.Win32.SaveFileDialog
            { FileName = dosyaAdi, DefaultExt = ".csv", Filter = "CSV Dosyası|*.csv" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var satirlar = new System.Collections.Generic.List<string> { "ID;Tutar;Ödeme Yöntemi;Tarih" };

                foreach (dynamic item in dgYikamalar.ItemsSource)
                    satirlar.Add($"{item.YikamaID};{item.Tutar};{item.Odeme};{item.Tarih}");

                satirlar.Add("");
                satirlar.Add("--- OZET BİLGİLER ---;;;");
                satirlar.Add($"Tarih Aralığı:;{dpYikamaBaslangic.SelectedDate.Value:dd.MM.yyyy} - {dpYikamaBitis.SelectedDate.Value:dd.MM.yyyy};;");
                satirlar.Add($"Toplam Gelir:;{lblYikamaToplam.Text};;");
                satirlar.Add($"Nakit Toplam:;{lblYikamaNakit.Text};;");
                satirlar.Add($"Kredi Toplam:;{lblYikamaKredi.Text};;");
                satirlar.Add($"Toplam Sayı:;{lblYikamaSayisi.Text};;");

                // 3. Dosyayı kaydet
                System.IO.File.WriteAllLines(dlg.FileName, satirlar, System.Text.Encoding.UTF8);
                // SQL'den sil
                DateTime bas = dpYikamaBaslangic.SelectedDate.Value.Date.AddHours(8);
                DateTime bitis = dpYikamaBitis.SelectedDate.Value.Date.AddDays(1).AddHours(3);
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand(
                    "DELETE FROM Yikamalar WHERE Tarih BETWEEN @bas AND @bitis", conn);
                cmd.Parameters.AddWithValue("@bas", bas);
                cmd.Parameters.AddWithValue("@bitis", bitis);
                cmd.ExecuteNonQuery();

                dgYikamalar.ItemsSource = null;
                lblYikamaToplam.Text = "—"; lblYikamaSayisi.Text = "—"; lblYikamaOrtalama.Text = "—";
                MessageBox.Show($"✅ Kayıtlar aktarıldı ve silindi:\n{dlg.FileName}", "Başarılı");
            }
            catch (Exception ex) { MessageBox.Show($"Hata: {ex.Message}"); }
        }
        private void btnYikamaIptal_Click(object sender, RoutedEventArgs e)
        {
            // 1. Listeden bir satır seçilmiş mi kontrol et
            if (dgYikamalar.SelectedItem == null)
            {
                MessageBox.Show("Lütfen iptal etmek istediğiniz yıkama kaydını listeden seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Seçili satırdan verileri al (Listeyi dinamik nesneyle doldurduğumuz için dynamic kullanıyoruz)
            dynamic secili = dgYikamalar.SelectedItem;
            int yikamaID = secili.YikamaID;
            string tutar = secili.Tutar;
            string tarih = secili.Tarih;

            // 3. Kazayla silmeyi önlemek için sağlam bir onay al
            var onay = MessageBox.Show(
                $"{tarih} tarihli {tutar} tutarındaki yıkama kaydını İPTAL ETMEK istediğinize emin misiniz?\n\nBu işlem kalıcıdır!",
                "Yıkama İptal Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Exclamation);

            if (onay != MessageBoxResult.Yes) return;

            // 4. Veritabanından kalıcı olarak sil
            try
            {
                using (var conn = new SqlConnection(_connStr))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("DELETE FROM Yikamalar WHERE YikamaID = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", yikamaID);
                        cmd.ExecuteNonQuery();
                    }

                    MessageBox.Show("✅ Yıkama kaydı başarıyla iptal edildi ve silindi!", "Başarılı");

                    // 5. Listeyi ve toplam rakamları otomatik yenilemek için rapor getirme butonunu tetikle
                    btnYikamaRaporGetir_Click(null, null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Silme işlemi sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // ─── YIKAMA RAPORU ────────────────────────────────────────────
        private void btnYikamaRaporGetir_Click(object sender, RoutedEventArgs e)
        {
            if (dpYikamaBaslangic.SelectedDate == null || dpYikamaBitis.SelectedDate == null) return;

            DateTime bas = dpYikamaBaslangic.SelectedDate.Value.Date.AddHours(8);
            DateTime bitis = dpYikamaBitis.SelectedDate.Value.Date.AddDays(1).AddHours(3);

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();

                var dt = new DataTable();
                using var cmd = new SqlCommand(
                    "SELECT YikamaID, Tutar, Tarih, ISNULL(OdemeYontemi, 'Belirtilmedi') AS OdemeYontemi FROM Yikamalar WHERE Tarih >= @bas AND Tarih <= @bitis ORDER BY Tarih DESC", conn);
                cmd.Parameters.AddWithValue("@bas", bas);
                cmd.Parameters.AddWithValue("@bitis", bitis);
                new SqlDataAdapter(cmd).Fill(dt);

                var liste = dt.AsEnumerable().Select(r => new
                {
                    YikamaID = r.Field<int>("YikamaID"),
                    Tutar = $"{r.Field<decimal>("Tutar"):N2} ₺",
                    Odeme = r.Field<string>("OdemeYontemi"),
                    Tarih = r.Field<DateTime>("Tarih").ToString("dd.MM.yyyy HH:mm")
                }).ToList();

                dgYikamalar.ItemsSource = liste;

                if (liste.Count > 0)
                {
                    decimal toplam = 0, nakit = 0, kredi = 0;

                    // Satırları tek tek dönüp ödeme yöntemini ayrıştırıyoruz
                    foreach (System.Data.DataRow r in dt.Rows)
                    {
                        decimal tutar = Convert.ToDecimal(r["Tutar"]);
                        string yontem = r["OdemeYontemi"].ToString();

                        toplam += tutar;

                        if (yontem == "Nakit")
                        {
                            nakit += tutar;
                        }
                        else if (yontem == "Kredi Kartı")
                        {
                            kredi += tutar;
                        }
                        else if (yontem.Contains("N") || yontem.Contains("K"))
                        {
                            // Parçalı ödemeyi çöz (Örn: 100 N 200 K)
                            nakit += ParcaliDegerCek(yontem, "N");
                            kredi += ParcaliDegerCek(yontem, "K");
                        }
                    }

                    lblYikamaToplam.Text = $"{toplam:N2} ₺";
                    lblYikamaNakit.Text = $"{nakit:N2} ₺";
                    lblYikamaKredi.Text = $"{kredi:N2} ₺";
                    lblYikamaSayisi.Text = $"{liste.Count} adet";
                    lblYikamaOrtalama.Text = $"{toplam / liste.Count:N2} ₺";
                }
                else
                {
                    lblYikamaToplam.Text = "0,00 ₺"; lblYikamaNakit.Text = "0,00 ₺"; lblYikamaKredi.Text = "0,00 ₺";
                    lblYikamaSayisi.Text = "0 adet"; lblYikamaOrtalama.Text = "—";
                }
            }
            catch (Exception ex) { MessageBox.Show($"Yıkama raporu hatası: {ex.Message}"); }
        }

        // ─── GRAFİKLER ────────────────────────────────────────────────
        private void btnEnCokSatan_Click(object sender, RoutedEventArgs e) =>
            GrafikCiz("ilk5getir_procedure", "Adet", "#4A7CF6");

        private void btnEnCokKar_Click(object sender, RoutedEventArgs e) =>
            GrafikCiz("EnCokKarGetirenler", "ToplamKar", "#27AE60");

        private void GrafikCiz(string procedure, string degerKolonu, string renk)
        {
            if (dpGrafBaslangic.SelectedDate == null || dpGrafBitis.SelectedDate == null) return;

            cnvGrafik.Children.Clear();
            var veriler = new List<(string Ad, double Deger)>();

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand(procedure, conn)
                { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@basTarihi", dpGrafBaslangic.SelectedDate.Value.Date.AddHours(8));
                cmd.Parameters.AddWithValue("@bitTarihi", dpGrafBitis.SelectedDate.Value.Date.AddDays(1).AddHours(3));
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    // Kolon adı yerine indeks kullan — SP kolon adına bağımlı değil
                    double deger = r.FieldCount > 1 ? Convert.ToDouble(r[1]) : 0;
                    veriler.Add((r[0].ToString(), deger));
                }
            }
            catch (Exception ex) { MessageBox.Show($"Grafik hatası: {ex.Message}"); return; }

            if (veriler.Count == 0) { lblGrafikBos.Visibility = Visibility.Visible; return; }

            lblGrafikBos.Visibility = Visibility.Collapsed;
            svGrafik.Visibility = Visibility.Visible;

            double maxDeger = veriler.Max(v => v.Deger);
            double barGenislik = 60;
            double aralik = 30;
            double maxYukseklik = 260;
            double toplam = veriler.Count * (barGenislik + aralik);

            cnvGrafik.Width = Math.Max(toplam + aralik, 600);

            for (int i = 0; i < veriler.Count; i++)
            {
                double x = aralik + i * (barGenislik + aralik);
                double oran = maxDeger > 0 ? veriler[i].Deger / maxDeger : 0;
                double yuksek = oran * maxYukseklik;
                double y = maxYukseklik - yuksek + 20;

                // Çubuk
                var bar = new Rectangle
                {
                    Width = barGenislik,
                    Height = yuksek,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(renk)!),
                    RadiusX = 6, RadiusY = 6, Opacity = 0.85
                };
                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, y);
                cnvGrafik.Children.Add(bar);

                // Değer etiketi
                var txtDeger = new TextBlock
                {
                    Text = degerKolonu == "Adet"
                        ? veriler[i].Deger.ToString("N0")
                        : $"{veriler[i].Deger:N0}₺",
                    Foreground = Brushes.White,
                    FontSize = 11, FontWeight = FontWeights.Bold,
                    Width = barGenislik, TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(txtDeger, x);
                Canvas.SetTop(txtDeger, y - 22);
                cnvGrafik.Children.Add(txtDeger);

                // Ürün adı
                var txtAd = new TextBlock
                {
                    Text = veriler[i].Ad.Length > 12
                        ? veriler[i].Ad.Substring(0, 12) + "..."
                        : veriler[i].Ad,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x99)),
                    FontSize = 10, Width = barGenislik + aralik,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };
                Canvas.SetLeft(txtAd, x - aralik / 2);
                Canvas.SetTop(txtAd, maxYukseklik + 28);
                cnvGrafik.Children.Add(txtAd);
            }
        }

        // ─── YENİ MARKA ───────────────────────────────────────────────
        private void btnYeniMarkaEkle_Click(object sender, RoutedEventArgs e)
        {
            string markaAdi = txtYeniMarka.Text.Trim();
            if (string.IsNullOrEmpty(markaAdi))
            {
                MessageBox.Show("Marka adı boş olamaz.", "Uyarı");
                return;
            }
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand(
                    "IF NOT EXISTS (SELECT 1 FROM Markalar WHERE MarkaAdi=@ad) " +
                    "INSERT INTO Markalar (MarkaAdi) VALUES (@ad)", conn);
                cmd.Parameters.AddWithValue("@ad", markaAdi);
                int etkilenen = cmd.ExecuteNonQuery();
                if (etkilenen == 0)
                    MessageBox.Show($"'{markaAdi}' markası zaten mevcut.", "Bilgi");
                else
                {
                    GosterMesaj(bdMarkaMesaj, lblMarkaMesaj, $"✅ '{markaAdi}' markası eklendi!");
                    txtYeniMarka.Clear();
                    MarkalariYukle();
                    // Yeni eklenen markayı seç
                    foreach (System.Data.DataRowView item in cmbYeniMarka.Items)
                        if (item["MarkaAdi"].ToString() == markaAdi)
                        { cmbYeniMarka.SelectedItem = item; break; }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Marka eklenemedi: {ex.Message}"); }
        }

        // ─── YARDIMCI ─────────────────────────────────────────────────
        private static void GosterMesaj(Border bd, TextBlock tb, string mesaj)
        {
            tb.Text = mesaj;
            bd.Visibility = Visibility.Visible;
        }
        // ─── KATEGORİ İŞLEMLERİ ───────────────────────────────────────────
        private void KategorileriDBdenYukle()
        {
            try
            {
                using var conn = new SqlConnection(_connStr);
                // "Turler" tablonun gerçek adına ve kolonlarına göre burayı ayarlayabilirsin.
                var da = new SqlDataAdapter("SELECT TurID, TurAdi FROM Turler ORDER BY TurAdi", conn);
                var dt = new DataTable();
                da.Fill(dt);

                // Tüm ComboBox'ları dinamik olarak güncelle
                cmbKategoriFiltre.ItemsSource = dt.DefaultView;
                cmbKategoriFiltre.DisplayMemberPath = "TurAdi";
                cmbKategoriFiltre.SelectedValuePath = "TurID";

                cmbYeniTur.ItemsSource = dt.DefaultView;
                cmbYeniTur.DisplayMemberPath = "TurAdi";
                cmbYeniTur.SelectedValuePath = "TurID";

                cmbKategoriSil.ItemsSource = dt.DefaultView;
                cmbKategoriSil.DisplayMemberPath = "TurAdi";
                cmbKategoriSil.SelectedValuePath = "TurID";

                if (cmbKategoriFiltre.Items.Count > 0) cmbKategoriFiltre.SelectedIndex = 0;
                if (cmbYeniTur.Items.Count > 0) cmbYeniTur.SelectedIndex = 0;

            }
            catch { /* Loglama eklenebilir */ }
        }

        private void btnKategoriEkle_Click(object sender, RoutedEventArgs e)
        {
            string yeniAd = txtYeniKategoriAdi.Text.Trim();
            if (string.IsNullOrEmpty(yeniAd)) { MessageBox.Show("Kategori adı boş olamaz!"); return; }

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand("INSERT INTO Turler (TurAdi) VALUES (@ad)", conn);
                cmd.Parameters.AddWithValue("@ad", yeniAd);
                cmd.ExecuteNonQuery();

                MessageBox.Show($"✅ '{yeniAd}' kategorisi başarıyla eklendi!", "Sistem");
                txtYeniKategoriAdi.Clear();
                KategorileriDBdenYukle(); // Listeleri yenile
            }
            catch (Exception ex) { MessageBox.Show($"Kategori eklenemedi: {ex.Message}"); }
        }

        private void btnKategoriSil_Click(object sender, RoutedEventArgs e)
        {
            if (cmbKategoriSil.SelectedValue == null) { MessageBox.Show("Lütfen silinecek kategoriyi seçin!"); return; }

            int seciliTurID = (int)cmbKategoriSil.SelectedValue;
            string seciliTurAdi = cmbKategoriSil.Text;

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();

                // ŞART KONTROLÜ: Bu kategoriye ait ürün var mı?
                using var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Urunler WHERE TurID = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", seciliTurID);
                int urunSayisi = (int)checkCmd.ExecuteScalar();

                if (urunSayisi > 0)
                {
                    MessageBox.Show(
                        $"⚠️ İŞLEM REDDEDİLDİ!\n\n'{seciliTurAdi}' kategorisine kayıtlı {urunSayisi} adet ürün bulunuyor.\nKategoriyi silmek için önce bu ürünleri silmeli veya başka kategoriye taşımalısınız.",
                        "Güvenlik Uyarısı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Ürün yoksa acıma, sil gitsin!
                using var deleteCmd = new SqlCommand("DELETE FROM Turler WHERE TurID = @id", conn);
                deleteCmd.Parameters.AddWithValue("@id", seciliTurID);
                deleteCmd.ExecuteNonQuery();

                MessageBox.Show($"🗑️ '{seciliTurAdi}' kategorisi kalıcı olarak silindi.", "Sistem");
                KategorileriDBdenYukle(); // Listeleri yenile
            }
            catch (Exception ex) { MessageBox.Show($"Silme hatası: {ex.Message}"); }
        }
        // ─── GÜVENLİK VE RAPOR KİLİDİ ──────────────────────────────────────────

        private async void tcAyarlar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // WPF'in gereksiz tetiklemelerini engelliyoruz
            if (e.OriginalSource != tcAyarlar) return;

            int seciliIndex = tcAyarlar.SelectedIndex;

            // Satış Raporu (3), Grafikler (4) ve Yıkama Raporu (5) sekmelerini koruyoruz
            if ((seciliIndex == 3 || seciliIndex == 4 || seciliIndex == 5) && !_raporYetkisiVar)
            {
                _hedefTabIndeks = seciliIndex;
                tcAyarlar.SelectedIndex = _sonSeciliTab;
                gridPinKilit.Visibility = Visibility.Visible;
                txtPinGiris.Clear();
                txtPinGiris.Focus();
                return;
            }

            _sonSeciliTab = tcAyarlar.SelectedIndex;

            // SMX Asistan sekmesi (index 2) açıldığında Telegram bağlantısını kontrol et
            if (seciliIndex == 2)
                await TelegramBaglantiKontrol();
        }

        private async Task TelegramBaglantiKontrol()
        {
            // Kontrol sırasında "Kontrol ediliyor..." göster, buton pasif kalsın
            txtTelegramDurumBaslik.Text = "⏳ Telegram Asistanı";
            txtTelegramDurumAlt.Text = "Kontrol ediliyor...";
            txtTelegramDurumBaslik.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            bdTelegramKart.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            bdTelegramKart.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2A, 0x1F));
            btnTelegramBildir.IsEnabled = false;

            bool bagli = false;
            try
            {
                string token = Properties.Settings.Default.TelegramToken;
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var resp = await client.GetAsync($"https://api.telegram.org/bot{token}/getMe");
                bagli = resp.IsSuccessStatusCode;
            }
            catch { bagli = false; }

            if (bagli)
            {
                txtTelegramDurumBaslik.Text = "🟢 Telegram Asistanı";
                txtTelegramDurumBaslik.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                bdTelegramKart.BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                bdTelegramKart.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x2D, 0x24));
                txtTelegramDurumAlt.Text = "Bağlantı Aktif";
                btnTelegramBildir.IsEnabled = true;
            }
            else
            {
                txtTelegramDurumBaslik.Text = "🔴 Telegram Asistanı";
                txtTelegramDurumBaslik.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                bdTelegramKart.BorderBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                bdTelegramKart.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x1F, 0x1F));
                txtTelegramDurumAlt.Text = "İnternet Bağlantısı Yok";
                btnTelegramBildir.IsEnabled = false;
            }
        }

        private void btnPinOnay_Click(object sender, RoutedEventArgs e)
        {
            if (txtPinGiris.Password == Properties.Settings.Default.RaporSifresi)
            {
                _raporYetkisiVar = true; // Yetkiyi ver
                gridPinKilit.Visibility = Visibility.Collapsed;
                tcAyarlar.SelectedIndex = _hedefTabIndeks; // Gitmek istediği sekmeye gönder
            }
            else
            {
                MessageBox.Show("Hatalı Şifre!", "Erişim Engellendi", MessageBoxButton.OK, MessageBoxImage.Error);
                txtPinGiris.Clear();
                txtPinGiris.Focus();
            }
        }

        private void txtPinGiris_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter) btnPinOnay_Click(null, null);
        }

        private void btnPinIptal_Click(object sender, RoutedEventArgs e)
        {
            gridPinKilit.Visibility = Visibility.Collapsed;
        }

        // ─── ŞİFRE DEĞİŞTİRME BUTONLARI ───

        private void btnGirisBilgiKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtYeniAdminID.Text) || string.IsNullOrWhiteSpace(txtYeniAdminSifre.Text))
            {
                MessageBox.Show("Giriş bilgileri boş bırakılamaz!"); return;
            }
            Properties.Settings.Default.KullaniciAdi = txtYeniAdminID.Text.Trim();
            Properties.Settings.Default.Sifre = txtYeniAdminSifre.Text.Trim();
            Properties.Settings.Default.Save();
            MessageBox.Show("✅ Ana giriş bilgileri başarıyla güncellendi!", "Başarılı");
        }

        private void btnRaporPinKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtYeniRaporPin.Text))
            {
                MessageBox.Show("Rapor şifresi boş olamaz!"); return;
            }
            Properties.Settings.Default.RaporSifresi = txtYeniRaporPin.Text.Trim();
            Properties.Settings.Default.Save();
            MessageBox.Show("✅ Rapor şifresi başarıyla güncellendi!", "Başarılı");
        }

        private void btnKapat_Click(object sender, RoutedEventArgs e) => Close();

        // ─── KAMPANYA YAP ─────────────────────────────────────────────
        private void btnKampanyaYap_Click(object sender, RoutedEventArgs e)
        {
            if (lstUrunler.SelectedItem is not ListBoxItem item || item.Tag is not CanliUrunModel urun)
            {
                MessageBox.Show("Kampanya uygulamak için önce listeden bir ürün seçin.", "Uyarı",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _kampanyaUrunID = urun.UrunID;
            _kampanyaEskiFiyat = 0;

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand("SELECT SatisFiyati FROM Urunler WHERE UrunID = @id", conn);
                cmd.Parameters.AddWithValue("@id", _kampanyaUrunID);
                _kampanyaEskiFiyat = Convert.ToDecimal(cmd.ExecuteScalar());
            }
            catch { }

            lblKampanyaUrunAdi.Text = $"{urun.UrunAdi}\nMevcut fiyat: {_kampanyaEskiFiyat:N2} ₺";
            txtKampanyaFiyat.Text   = "";
            bdKampanyaPopup.Visibility = Visibility.Visible;
            txtKampanyaFiyat.Focus();
        }

        private void btnKampanyaIptal_Click(object sender, RoutedEventArgs e)
        {
            bdKampanyaPopup.Visibility = Visibility.Collapsed;
        }

        private void txtKampanyaFiyat_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter) btnKampanyaOnayla_Click(null, null);
            if (e.Key == Key.Escape) btnKampanyaIptal_Click(null, null);
        }

        private void btnKampanyaOnayla_Click(object sender, RoutedEventArgs e)
        {
            string girdi = txtKampanyaFiyat.Text.Trim().Replace(",", ".");
            if (!decimal.TryParse(girdi, System.Globalization.NumberStyles.Any,
                                  System.Globalization.CultureInfo.InvariantCulture, out decimal yeniFiyat)
                || yeniFiyat <= 0)
            {
                MessageBox.Show("Lütfen geçerli bir fiyat girin (örn: 125 veya 125,50).", "Uyarı");
                return;
            }

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand(
                    "UPDATE Urunler SET SatisFiyati = @fiyat WHERE UrunID = @id", conn);
                cmd.Parameters.AddWithValue("@fiyat", yeniFiyat);
                cmd.Parameters.AddWithValue("@id", _kampanyaUrunID);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fiyat güncellenemedi: {ex.Message}", "Hata");
                return;
            }

            bdKampanyaPopup.Visibility = Visibility.Collapsed;
            MessageBox.Show(
                $"✅ Fiyat başarıyla güncellendi!\n\nEski Fiyat: {_kampanyaEskiFiyat:N2} ₺\nYeni Fiyat: {yeniFiyat:N2} ₺",
                "Kampanya Uygulandı", MessageBoxButton.OK, MessageBoxImage.Information);

            VeritabanindanCanliVerileriCek();
        }

        // ─── AKILLI ASİSTAN (API'SİZ) ──────────────────────────────────
        private void lstUrunler_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstUrunler.SelectedItem is not ListBoxItem item || item.Tag is not CanliUrunModel urun) return;

            string cacheKey = $"{urun.UrunID}_{_aktifListeTipi}";
            if (_asistanCache.TryGetValue(cacheKey, out var cached) &&
                (DateTime.Now - cached.Zaman).TotalMinutes < 10)
            {
                txtAsistanYorumu.Text = cached.Yanit;
                return;
            }

            string yanit = AkilliOneriUret(urun, _aktifListeTipi);
            txtAsistanYorumu.Text = yanit;
            _asistanCache[cacheKey] = (yanit, DateTime.Now);
        }

        private string AkilliOneriUret(CanliUrunModel urun, string tip)
        {
            int toplamSatis = 0;
            int gunAralik = 0;
            int sonSatisGun = 0;

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    SELECT COUNT(*)         AS ToplamSatis,
                           MIN(SatisTarihi) AS IlkSatis,
                           MAX(SatisTarihi) AS SonSatis
                    FROM SatislarAnaliz
                    WHERE UrunID = @id", conn);
                cmd.Parameters.AddWithValue("@id", urun.UrunID);
                using var r = cmd.ExecuteReader();
                if (r.Read() && Convert.ToInt32(r["ToplamSatis"]) > 0)
                {
                    toplamSatis = Convert.ToInt32(r["ToplamSatis"]);
                    DateTime ilk = Convert.ToDateTime(r["IlkSatis"]);
                    DateTime son = Convert.ToDateTime(r["SonSatis"]);
                    int gecenGun = Math.Max(1, (int)(DateTime.Today - ilk).TotalDays);
                    gunAralik  = toplamSatis > 1 ? Math.Max(1, (int)((son - ilk).TotalDays / (toplamSatis - 1))) : gecenGun;
                    sonSatisGun = (int)(DateTime.Today - son).TotalDays;
                }
            }
            catch { }

            var sb = new System.Text.StringBuilder();

            string baslik = tip == "kritik" ? "🔴 Kritik Stok Analizi" : "🟡 Hareketsiz Ürün Analizi";
            sb.AppendLine($"{baslik} — {urun.UrunAdi}");
            if (toplamSatis > 0)
                sb.AppendLine($"📈 Son 6 ay: {toplamSatis} satış | Ort. her {gunAralik} günde bir | Son satış: {sonSatisGun} gün önce");
            else
                sb.AppendLine("📈 Satış geçmişi kaydı bulunmuyor.");
            sb.AppendLine();

            if (tip == "kritik")
            {
                if (toplamSatis > 0)
                {
                    int tahminiGun = gunAralik > 0 ? urun.MevcutStok * gunAralik : 0;
                    sb.AppendLine($"▸ Mevcut {urun.MevcutStok} adet, satış hızınıza göre yaklaşık {tahminiGun} gün içinde tükenecek. Bu tarihe kadar tedarik siparişi verilmesi önerilir.");
                }
                else
                {
                    sb.AppendLine($"▸ Bu üründe satış geçmişi bulunmuyor; stokta yalnızca {urun.MevcutStok} adet kalmış. Tedarikçinizi en kısa sürede arayarak sipariş verin.");
                }
                sb.AppendLine($"▸ Stok tükenmeden önce en az {Math.Max(5, urun.MevcutStok + 5)} adetlik tampon stok oluşturun. Tükenme yaşanırsa müşteri rakip firmaya yönelebilir.");
                sb.AppendLine($"▸ Ürünü tedarikçi siparişinde öncelikli listeye alın ve mümkünse ekspres teslimat talep edin.");
            }
            else
            {
                if (toplamSatis == 0)
                {
                    sb.AppendLine($"▸ Bu ürün hiç satılmamış. Fiyatını rakip firmalarla karşılaştırın; rakipler daha düşük fiyat sunuyorsa %10-15 oranında indirim değerlendirin.");
                    sb.AppendLine($"▸ Ürünü ön vitrine veya ödeme kasasının yanına taşıyın — müşteri gözüne çarpan ürünlerin satışı belirgin şekilde artmaktadır.");
                    sb.AppendLine($"▸ Stokta {urun.MevcutStok} adetlik bağlı sermaye bulunuyor. Bir sonraki siparişte bu üründen ek alım yapmamanız önerilir.");
                }
                else if (sonSatisGun > 60)
                {
                    sb.AppendLine($"▸ Son satışın üzerinden {sonSatisGun} gün geçmiş. Ürün üzerine %15-20 indirimli fiyat etiketi koyarak müşteri ilgisini yeniden çekin.");
                    sb.AppendLine($"▸ Çok satan ürünlerle paket kampanyası oluşturun (örn. Motor Yağı ile birlikte alana indirim). Paket teklifleri hareketsiz ürün satışını canlandırır.");
                    sb.AppendLine($"▸ 60 gün daha satış gerçekleşmezse {urun.MevcutStok} adedi tedarikçiye iade etmeyi ya da büyük indirimle elden çıkarmayı değerlendirin.");
                }
                else
                {
                    sb.AppendLine($"▸ Son {sonSatisGun} gündür hareketsiz. Fiyat etiketini gözden geçirin ve rakiplerin fiyatlarıyla karşılaştırın; küçük bir fark bile satışı tetikleyebilir.");
                    sb.AppendLine($"▸ Servis ve ödeme sırasında müşterilere bu ürünü aktif olarak önerin; kişisel tavsiye dönüşüm oranını önemli ölçüde artırır.");
                    sb.AppendLine($"▸ Mevcut {urun.MevcutStok} adedin yarısını kısa süreli kampanya fiyatıyla öne çıkararak stok hareketi başlatın.");
                }
            }

            return sb.ToString().TrimEnd();
        }

        private void btnStokExport_Click(object sender, RoutedEventArgs e)
        {
            // 1. Sıralama kriterini al
            string siralamaSql = "ORDER BY m.MarkaAdi, u.UrunAdi"; // Varsayılan Alfabetik
            if (cmbStokSiralama.SelectedItem is ComboBoxItem secili)
            {
                switch (secili.Tag.ToString())
                {
                    case "StokAz": siralamaSql = "ORDER BY u.MevcutStok DESC"; break;
                    case "StokArt": siralamaSql = "ORDER BY u.MevcutStok ASC"; break;
                }
            }

            // 2. Kayıt penceresini aç
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"Stok_Raporu_{DateTime.Now:dd.MM.yyyy}",
                DefaultExt = ".csv",
                Filter = "CSV Dosyası|*.csv"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();

                // Tüm ürünleri detaylıca çekiyoruz
                string query = $@"
            SELECT m.MarkaAdi, u.UrunAdi, t.TurAdi, u.MevcutStok, u.AlisFiyati, u.SatisFiyati, u.Barkod
            FROM Urunler u
            JOIN Markalar m ON u.MarkaID = m.MarkaID
            JOIN Turler t ON u.TurID = t.TurID
            {siralamaSql}";

                var dt = new DataTable();
                new SqlDataAdapter(query, conn).Fill(dt);

                // 3. CSV içeriğini oluştur
                var satirlar = new List<string> { "Marka;Urun Adi;Kategori;Mevcut Stok;Alis Fiyatı;Satis Fiyatı;Barkod" };

                foreach (DataRow row in dt.Rows)
                {
                    satirlar.Add($"{row["MarkaAdi"]};{row["UrunAdi"]};{row["TurAdi"]};{row["MevcutStok"]};" +
                                 $"{row["AlisFiyati"]:N2} ₺;{row["SatisFiyati"]:N2} ₺;{row["Barkod"]}");
                }

                // 4. Dosyaya yaz (UTF8 desteğiyle)
                System.IO.File.WriteAllLines(dlg.FileName, satirlar, System.Text.Encoding.UTF8);

                MessageBox.Show($"✅ Toplam {dt.Rows.Count} ürünlük stok raporu başarıyla kaydedildi!",
                                "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Stok raporu oluşturulurken hata oluştu: {ex.Message}", "Hata");
            }
        }
        private void BorderKritikStok_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _aktifListeTipi = "kritik";
            lblListeBasligi.Text = "🚨 İncelenecek Kritik Stoklu Ürünler";
            lstUrunler.Items.Clear();

            if (_canliKritikUrunler.Count > 0)
            {
                foreach (var urun in _canliKritikUrunler)
                {
                    ListBoxItem urunItem = new ListBoxItem { Padding = new Thickness(10) };
                    StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };

                    panel.Children.Add(new TextBlock { Text = $"📦 {urun.UrunAdi}", FontWeight = FontWeights.Bold, FontSize = 15, Width = 200, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis });

                    string durumYazisi = urun.MevcutStok == 0 ? "🛑 TÜKENDİ" : $"⚠️ {urun.MevcutStok} adet kaldı";
                    panel.Children.Add(new TextBlock { Text = durumYazisi, Foreground = urun.MevcutStok == 0 ? Brushes.Crimson : Brushes.Orange, Width = 160, FontSize = 14, FontWeight = FontWeights.SemiBold });

                    panel.Children.Add(new TextBlock { Text = "Acil Tedarik Gerekli", Foreground = Brushes.YellowGreen, FontSize = 13 });

                    urunItem.Tag = urun;
                    urunItem.Content = panel;
                    lstUrunler.Items.Add(urunItem);
                }
                txtAsistanYorumu.Text = "Listeden bir ürün seçin — Asistan o ürün için stok ve satış analizine dayalı öneriler sunsun.";
            }
            else
            {
                ListBoxItem bosItem = new ListBoxItem { Padding = new Thickness(10) };
                bosItem.Content = new TextBlock { Text = "✅ Tüm ürünler yeterli stok seviyesinde. Kritik durum tespit edilmedi.", Foreground = Brushes.LightGreen, FontSize = 14 };
                lstUrunler.Items.Add(bosItem);
                txtAsistanYorumu.Text = "Tüm ürünlerin stok seviyeleri yeterli. Herhangi bir tedarik aksiyonu gerekmemektedir.";
            }
        }
        private void BorderHareketsizUrunler_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _aktifListeTipi = "hareketsiz";
            lblListeBasligi.Text = "⏳ İncelenecek Hareketsiz Ürünler";
            lstUrunler.Items.Clear();

            if (_canliHareketsizUrunler.Count > 0)
            {
                foreach (var urun in _canliHareketsizUrunler)
                {
                    ListBoxItem urunItem = new ListBoxItem { Padding = new Thickness(10) };
                    StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };

                    panel.Children.Add(new TextBlock
                    {
                        Text = $"🛠️ {urun.UrunAdi}",
                        FontWeight = FontWeights.Bold,
                        FontSize = 15,
                        Width = 200,
                        Foreground = Brushes.White,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });

                    panel.Children.Add(new TextBlock
                    {
                        Text = $"Stok: {urun.MevcutStok} adet",
                        Foreground = Brushes.Silver,
                        FontSize = 14,
                        Width = 110
                    });

                    panel.Children.Add(new TextBlock
                    {
                        Text = "30+ gün hareketsiz",
                        Foreground = Brushes.Orange,
                        FontSize = 13,
                        Width = 130
                    });

                    panel.Children.Add(new TextBlock
                    {
                        Text = "Aksiyon Gerekli",
                        Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                        FontSize = 13
                    });

                    urunItem.Tag = urun;
                    urunItem.Content = panel;
                    lstUrunler.Items.Add(urunItem);
                }

                txtAsistanYorumu.Text = "Listeden bir ürün seçin — Asistan o ürün için hareketsizlik analizine dayalı öneriler sunsun.";
            }
            else
            {
                ListBoxItem bosItem = new ListBoxItem { Padding = new Thickness(10) };
                bosItem.Content = new TextBlock { Text = "✅ Son 30 günde tüm ürünlerden en az bir satış gerçekleşti. Hareketsiz ürün bulunmuyor.", Foreground = Brushes.LightGreen, FontSize = 14 };
                lstUrunler.Items.Add(bosItem);
                txtAsistanYorumu.Text = "Tüm ürünler aktif olarak satılıyor. Hareketsiz stok kaydı bulunmuyor.";
            }
        }
        private void VeritabanindanCanliVerileriCek()
        {
            _canliKritikUrunler.Clear();
            _canliHareketsizUrunler.Clear();

            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();

                // --- A) KRİTİK STOK SORGUSU ---
                string kritikStokQuery = @"SELECT u.UrunID, m.MarkaAdi + ' ' + u.UrunAdi AS UrunAdi, u.MevcutStok
                                           FROM Urunler u JOIN Markalar m ON u.MarkaID = m.MarkaID
                                           WHERE u.MevcutStok <= 3 ORDER BY u.MevcutStok ASC";
                using (var cmd = new SqlCommand(kritikStokQuery, conn))
                {
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        _canliKritikUrunler.Add(new CanliUrunModel
                        {
                            UrunID     = Convert.ToInt32(reader["UrunID"]),
                            UrunAdi    = reader["UrunAdi"].ToString(),
                            MevcutStok = Convert.ToInt32(reader["MevcutStok"])
                        });
                    }
                }

                // Not: Sabah toplu bildirimi MainWindow açılışında gönderilir.

                // --- B) HAREKETSİZ ÜRÜNLER SORGUSU ---
                // Tablo en az 30 günlük veriye sahipse kontrol başlar; boş/yeni tabloda yanlış sonuç vermez.
                string hareketsizQuery = @"
                    SELECT u.UrunID, m.MarkaAdi + ' ' + u.UrunAdi AS UrunAdi, u.MevcutStok
                    FROM Urunler u JOIN Markalar m ON u.MarkaID = m.MarkaID
                    WHERE u.MevcutStok > 0
                      AND u.UrunID NOT IN (
                          SELECT DISTINCT UrunID FROM SatislarAnaliz
                          WHERE SatisTarihi >= DATEADD(day, -30, GETDATE())
                      )
                      AND DATEDIFF(day,
                              ISNULL((SELECT MIN(SatisTarihi) FROM SatislarAnaliz), GETDATE()),
                              GETDATE()) >= 30
                    ORDER BY u.MevcutStok DESC";

                using (var cmd = new SqlCommand(hareketsizQuery, conn))
                {
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        _canliHareketsizUrunler.Add(new CanliUrunModel
                        {
                            UrunID     = Convert.ToInt32(reader["UrunID"]),
                            UrunAdi    = reader["UrunAdi"].ToString(),
                            MevcutStok = Convert.ToInt32(reader["MevcutStok"])
                        });
                    }
                }

                // Üst sayaç kutularının güncellenmesi
                txtKritikStokAnalizi.Text = _canliKritikUrunler.Count > 0
                    ? $"Tükenmek üzere {_canliKritikUrunler.Count} ürün var!"
                    : "Stoklar yeterli, sorun yok ✅";
                txtHareketsizUrunAnalizi.Text = _canliHareketsizUrunler.Count > 0
                    ? $"30+ gündür satılmayan {_canliHareketsizUrunler.Count} ürün var!"
                    : "Tüm ürünler hareketli ✅";

                // İlk açılışta ekrana kritik stok listesini verelim
                BorderKritikStok_MouseDown(null, null);

                EnCokSatanlariYukle();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Veritabanı Hatası: {ex.Message}");
            }
        }

        private void EnCokSatanlariYukle()
        {
            pnlEnCokSatan.Children.Clear();
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    SELECT TOP 5 m.MarkaAdi + ' ' + u.UrunAdi AS UrunAdi, COUNT(*) AS Adet
                    FROM SatislarAnaliz sa
                    JOIN Urunler u ON sa.UrunID = u.UrunID
                    JOIN Markalar m ON u.MarkaID = m.MarkaID
                    WHERE sa.SatisTarihi >= DATEADD(day, -7, GETDATE())
                    GROUP BY u.UrunID, m.MarkaAdi, u.UrunAdi
                    ORDER BY Adet DESC", conn);
                using var r = cmd.ExecuteReader();
                int sira = 1;
                while (r.Read())
                {
                    string ad   = r["UrunAdi"].ToString();
                    int    adet = Convert.ToInt32(r["Adet"]);
                    string kisa = ad.Length > 18 ? ad.Substring(0, 18) + "…" : ad;

                    var kart = new Border
                    {
                        Background    = new SolidColorBrush(Color.FromRgb(0x25, 0x2A, 0x3A)),
                        CornerRadius  = new CornerRadius(8),
                        Padding       = new Thickness(12, 7, 12, 7),
                        Margin        = new Thickness(0, 0, 10, 0)
                    };
                    var sp = new StackPanel();
                    sp.Children.Add(new TextBlock
                    {
                        Text       = $"#{sira}  {kisa}",
                        Foreground = Brushes.White,
                        FontSize   = 12,
                        FontWeight = FontWeights.SemiBold
                    });
                    sp.Children.Add(new TextBlock
                    {
                        Text       = $"{adet} adet satıldı",
                        Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                        FontSize   = 11
                    });
                    kart.Child = sp;
                    pnlEnCokSatan.Children.Add(kart);
                    sira++;
                }

                if (pnlEnCokSatan.Children.Count == 0)
                    pnlEnCokSatan.Children.Add(new TextBlock
                    {
                        Text       = "Son 7 günde henüz satış kaydı bulunmuyor.",
                        Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x99)),
                        FontSize   = 12
                    });
            }
            catch { }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            string tarih = DateTime.Now.ToString("dd.MM.yyyy HH:mm", new System.Globalization.CultureInfo("tr-TR"));
            string mesaj = $"📊 *NW NEON OTO BAKIM — ASİSTAN RAPORU*\n" +
                           $"━━━━━━━━━━━━━━━━━━━━━━\n" +
                           $"📅 *Tarih:* {tarih}\n" +
                           $"━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                           txtAsistanYorumu.Text;
            try
            {
                string botToken = Properties.Settings.Default.TelegramToken;
                string chatId   = Properties.Settings.Default.TelegramChatId;
                string url = $"https://api.telegram.org/bot{botToken}/sendMessage";
                var payload = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "chat_id", chatId },
                    { "text",    mesaj  },
                    { "parse_mode", "Markdown" }
                };
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var response = await client.PostAsync(url, new System.Net.Http.FormUrlEncodedContent(payload));
                if (response.IsSuccessStatusCode)
                    System.Windows.MessageBox.Show("📱 Asistan raporu telefona başarıyla iletildi!", "Telegram",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                else
                {
                    string hata = await response.Content.ReadAsStringAsync();
                    System.Windows.MessageBox.Show($"Telegram yanıtı: HTTP {(int)response.StatusCode}\n{hata}", "Gönderim Hatası",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Bağlantı hatası: {ex.Message}", "Hata",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        private async Task HareketsizUrunlerBildirimGonder(List<CanliUrunModel> hareketsizUrunler)
        {
            string tarih = DateTime.Now.ToString("dd.MM.yyyy HH:mm", new System.Globalization.CultureInfo("tr-TR"));

            string mesaj = $"🟡 *NW NEON OTO BAKIM — HAREKETSİZ ÜRÜN RAPORU*\n" +
                           $"━━━━━━━━━━━━━━━━━━━━━━\n" +
                           $"📅 *Tarih:* {tarih}\n" +
                           $"🔢 *Hareketsiz Ürün Sayısı:* {hareketsizUrunler.Count} kalem\n" +
                           $"⏳ *Kapsam:* Son 30 gün\n" +
                           $"━━━━━━━━━━━━━━━━━━━━━━\n\n";

            foreach (var urun in hareketsizUrunler)
                mesaj += $"▸ {urun.UrunAdi} — Stok: {urun.MevcutStok} adet\n";

            mesaj += $"\n━━━━━━━━━━━━━━━━━━━━━━\n" +
                     $"💡 _Bu ürünlerdeki sermayeyi hızlı satılan ürünlere aktarmanızı öneririz._";

            await TelegramGonder(mesaj);
        }

        private async Task TopluStokUyarisiGonder(List<CanliUrunModel> kritikUrunler)
        {
            string tarih = DateTime.Now.ToString("dd.MM.yyyy HH:mm", new System.Globalization.CultureInfo("tr-TR"));

            string mesaj = $"🚨 *NW NEON OTO BAKIM — KRİTİK STOK RAPORU*\n" +
                           $"━━━━━━━━━━━━━━━━━━━━━━\n" +
                           $"📅 *Tarih:* {tarih}\n" +
                           $"🔢 *Kritik Ürün Sayısı:* {kritikUrunler.Count} kalem\n" +
                           $"━━━━━━━━━━━━━━━━━━━━━━\n\n";

            foreach (var urun in kritikUrunler)
            {
                string durum = urun.MevcutStok == 0 ? "🛑 *TÜKENDİ*" : $"⚠️ *{urun.MevcutStok} adet* kaldı";
                mesaj += $"▸ {urun.UrunAdi} — {durum}\n";
            }

            mesaj += $"\n━━━━━━━━━━━━━━━━━━━━━━\n" +
                     $"💡 _Müşteri kaybetmemek için acilen tedarikçinizi arayınız._";

            await TelegramGonder(mesaj);
        }

        private static async Task TelegramGonder(string mesaj)
        {
            string botToken = Properties.Settings.Default.TelegramToken;
            string chatId   = Properties.Settings.Default.TelegramChatId;
            string url = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}" +
                         $"&text={Uri.EscapeDataString(mesaj)}&parse_mode=Markdown";
            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                await client.GetAsync(url);
            }
            catch { }
        }

       
        private void KritikStokListesiniYenile()
        {
            lblListeBasligi.Text = "🚨 İncelenecek Kritik Stoklu Ürünler";
            lstUrunler.Items.Clear();

            if (_canliKritikUrunler.Count > 0)
            {
                foreach (var urun in _canliKritikUrunler)
                {
                    lstUrunler.Items.Add(new ListBoxItem { Content = urun, Padding = new Thickness(10) });
                }
            }
            else
            {
                lstUrunler.Items.Add(new ListBoxItem { Content = "✅ Harika! Kritik seviyede ürün bulunmuyor.", Padding = new Thickness(10) });
            }
        }
        private void HareketsizUrunleriListesiniYenile()
        {
            lblListeBasligi.Text = "⏳ İncelenecek Hareketsiz Ürünler";
            lstUrunler.Items.Clear();

            if (_canliHareketsizUrunler.Count > 0)
            {
                foreach (var urun in _canliHareketsizUrunler)
                {
                    lstUrunler.Items.Add(new ListBoxItem { Content = urun, Padding = new Thickness(10) });
                }
            }
            else
            {
                lstUrunler.Items.Add(new ListBoxItem { Content = "✅ Mükemmel! Son 30 günde tüm ürünleriniz satıldı.", Padding = new Thickness(10) });
            }
        }
    }

    }










