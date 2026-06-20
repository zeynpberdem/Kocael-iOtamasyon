using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace per101
{
    public partial class ÜRÜNLER : Form
    {
        private void ToplamHesapla()
        {
            decimal genelToplam = 0;

            foreach (ListViewItem item in listView2.Items)
            {
                // 4. sütunda (indeks 3) veri var mı ve boş değil mi kontrol et
                if (item.SubItems.Count > 3 && !string.IsNullOrEmpty(item.SubItems[3].Text))
                {

                    genelToplam += Convert.ToDecimal(item.SubItems[3].Text);
                }
            }

           
            label4.Text = genelToplam.ToString("N2") + " TL";
        }
        private void SepeteEkle()
        {
            if (listView1.SelectedItems.Count > 0)
            {
                ListViewItem secilen = listView1.SelectedItems[0];

                // KRİTİK KONTROL: Eğer secilen.Tag boşsa kontrol patlar.
                if (secilen.Tag == null)
                {
                    MessageBox.Show("Hata: Ürün ID'si bulunamadı!");
                    return;
                }

                string secilenID = secilen.Tag.ToString();

               
                foreach (ListViewItem sepetItem in listView2.Items)
                {
                    
                    if (sepetItem.Tag != null && sepetItem.Tag.ToString() == secilenID)
                    {
                        MessageBox.Show("Bu ürün zaten sepette var!", "Uyarı");
                        return; // Bulunca direkt çık, aşağıya inme!
                    }
                }

                // Eğer buraya kadar geldiyse ürün sepette yoktur, ekle:
                ListViewItem yeniSepetItem = new ListViewItem(secilen.SubItems[0].Text);
                yeniSepetItem.SubItems.Add(secilen.SubItems[1].Text);
                yeniSepetItem.SubItems.Add("1");
                yeniSepetItem.SubItems.Add(secilen.SubItems[1].Text);

                yeniSepetItem.Tag = secilenID;
                listView2.Items.Add(yeniSepetItem);
                ToplamHesapla();
            }
        }
        public ÜRÜNLER()
        {
            InitializeComponent();
        }

        private void kasa_Load(object sender, EventArgs e) { }
        private void groupBox1_Enter(object sender, EventArgs e) { }

        private void listView2_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
        SqlConnection baglanti = new SqlConnection(@"Data Source=MertPC\SQLEXPRESS;Initial Catalog=per10Database;User ID=sa;Password=1;Encrypt=True;TrustServerCertificate=True");
        private void button1_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();

            // 2. Bağlantı cümlemiz (Senin kurduğun bağlantı nesnesini kullanıyoruz)
            string query = "SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.SatisFiyati, u.MevcutStok " +
               "FROM Urunler u " +
               "JOIN Markalar m ON u.MarkaID = m.MarkaID " +
               "WHERE u.TurID = 1";

            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                SqlCommand komut = new SqlCommand(query, baglanti);
                SqlDataReader oku = komut.ExecuteReader();

                while (oku.Read())
                {

                   if (Convert.ToInt32(oku["MevcutStok"]) > 0)
                    {
                    ListViewItem ekle = new ListViewItem(oku["MarkaAdi"].ToString() + " " + oku["UrunAdi"].ToString());
                    ekle.SubItems.Add(oku["SatisFiyati"].ToString());
                    ekle.SubItems.Add(oku["MevcutStok"].ToString());
                    ekle.Tag = oku["UrunID"].ToString();
                    // Listeye ekliyoruz
                    listView1.Items.Add(ekle);
                    }
                   
                }
                oku.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veri çekme hatası: " + ex.Message);
            }
            finally
            {
                baglanti.Close();
            }
        }

        private void ÜRÜNLER_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult sonuc = MessageBox.Show("Programı kapatmak istediğinize emin misiniz?", "Çıkış Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (sonuc == DialogResult.No)
            {
                e.Cancel = true; // Kapanma işlemini iptal eder, form açık kalır.
            }

        }

        private void ÜRÜNLER_FormClosed(object sender, FormClosedEventArgs e) { Application.Exit(); }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e) { SepeteEkle(); }

        
        private void button22_Click_1(object sender, EventArgs e) // sepetteki ürün adedini arttır
        {
            if (listView2.SelectedItems.Count > 0)
            {
                ListViewItem seciliSepetUrünü = listView2.SelectedItems[0];

           
                string urunID = seciliSepetUrünü.Tag.ToString();

                
                int guncelStok = 0;
               
                    baglanti.Open();
                    string stokSorgusu = "SELECT MevcutStok FROM Urunler WHERE UrunID = @id";
                    using (SqlCommand komut = new SqlCommand(stokSorgusu, baglanti))
                    {
                        komut.Parameters.AddWithValue("@id", urunID);
                        guncelStok = Convert.ToInt32(komut.ExecuteScalar());
                    }      
                    
                int sepettekiAdet = int.Parse(seciliSepetUrünü.SubItems[2].Text);
                decimal  uruntoplam= decimal.Parse(seciliSepetUrünü.SubItems[3].Text);
                decimal urunfiyat = decimal.Parse(seciliSepetUrünü.SubItems[1].Text);

                if (sepettekiAdet < guncelStok)
                {
                    sepettekiAdet++;
                    uruntoplam = sepettekiAdet * urunfiyat;
                    seciliSepetUrünü.SubItems[3].Text = uruntoplam.ToString();
                    ToplamHesapla();
                    seciliSepetUrünü.SubItems[2].Text = sepettekiAdet.ToString();
                  
                }
                else
                {
                    MessageBox.Show($"Stok yetersiz! Maksimum stok: {guncelStok}", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show("Lütfen sepetten bir ürün seçin.");
            }
            baglanti.Close();
        }

        private void button21_Click(object sender, EventArgs e)
        {
            decimal indirim = Convert.ToDecimal(textBox2.Text);
            string temizMetin = label4.Text.Replace("Toplam Tutar: ", "").Replace(" TL", "").Trim();
            decimal fiyat = Convert.ToDecimal(temizMetin);
            decimal sonfiyat = fiyat - indirim;
            label4.Text = Convert.ToString(sonfiyat.ToString() + " TL");
            textBox2.Clear();
        }

        private void adetsil_Click(object sender, EventArgs e)
        {
            if (listView2.SelectedItems.Count > 0)
            {
                // 2. Seçili olan satırı sepetten (listView2) kaldır
                listView2.Items.Remove(listView2.SelectedItems[0]);

                // 3. KRİTİK ADIM: Ürün silindiği için toplam tutarı yeniden hesapla [cite: 2026-01-30]
                ToplamHesapla();

                // 4. Kullanıcıya geri bildirim (isteğe bağlı ama şık durur)
                // label4 zaten ToplamHesapla içinde güncelleniyor [cite: 2026-01-30]
            }
            else
            {
                MessageBox.Show("Lütfen önce sepetten silmek istediğiniz ürünü seçin!", "Uyarı");
            }
        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        private void odemeButon_Click(object sender, EventArgs e)
        {

            if (listView2.Items.Count == 0) return;

            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                // A. Önce Sepet oluştur ve yeni SepetID'yi al
                int yeniSepetID = 0;
                string sepetSorgu = "INSERT INTO Sepetler (Tarih) VALUES (GETDATE()); SELECT SCOPE_IDENTITY();";
                SqlCommand cmdSepet = new SqlCommand(sepetSorgu, baglanti);
                yeniSepetID = Convert.ToInt32(cmdSepet.ExecuteScalar());

                // B. Sepetteki her ürünü bu SepetID ile kaydet
                foreach (ListViewItem item in listView2.Items)
                {
                    int urunID = Convert.ToInt32(item.Tag);
                    int miktar = Convert.ToInt32(item.SubItems[2].Text);
                    decimal satis = Convert.ToDecimal(item.SubItems[1].Text);

                    // Alış fiyatını çekme kodun burada devam edebilir (önceki gibi)...
                    decimal alis = 20; // Örnek sabit, sen SQL'den çekiyorsun

                    string query = "INSERT INTO Satislar (UrunID, Miktar, birimalisfiyati, birimsatisfiyati, SatisTarihi, SepetID) " +
                                   "VALUES (@id, @miktar, @alis, @satis, GETDATE(), @sepetId)";

                    using (SqlCommand komut = new SqlCommand(query, baglanti))
                    {
                        komut.Parameters.AddWithValue("@id", urunID);
                        komut.Parameters.AddWithValue("@miktar", miktar);
                        komut.Parameters.AddWithValue("@alis", alis);
                        komut.Parameters.AddWithValue("@satis", satis);
                        komut.Parameters.AddWithValue("@sepetId", yeniSepetID); // İşte o kritik bağ!
                        komut.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Ödeme Başarılı!");
                listView2.Items.Clear();
                ToplamHesapla();
            }
            catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message); }
            finally { baglanti.Close(); }
        }

        private void satisgeri_Click(object sender, EventArgs e)
        {
            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                // En son oluşturulan SEPETİN ID'sini bul
                string sonSepetSorgu = "SELECT MAX(SepetID) FROM Sepetler";
                SqlCommand cmd = new SqlCommand(sonSepetSorgu, baglanti);
                object res = cmd.ExecuteScalar();

                if (res != DBNull.Value)
                {
                    int sonSepetID = Convert.ToInt32(res);
                    SqlCommand cmdSil = new SqlCommand("sp_SepetIptal", baglanti);
                    cmdSil.CommandType = CommandType.StoredProcedure;
                    cmdSil.Parameters.AddWithValue("@SepetID", sonSepetID);
                    cmdSil.ExecuteNonQuery();

                    MessageBox.Show("Son yapılan sepet ve içindeki tüm ürünler iptal edildi, stoklar geri yüklendi!");
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally { baglanti.Close(); }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();

            // 2. Bağlantı cümlemiz (Senin kurduğun bağlantı nesnesini kullanıyoruz)
            string query = "SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.SatisFiyati, u.MevcutStok " +
               "FROM Urunler u " +
               "JOIN Markalar m ON u.MarkaID = m.MarkaID " +
               "WHERE u.TurID = 2";

            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                SqlCommand komut = new SqlCommand(query, baglanti);
                SqlDataReader oku = komut.ExecuteReader();

                while (oku.Read())
                {


                    if (Convert.ToInt32(oku["MevcutStok"]) > 0)
                    {
                        ListViewItem ekle = new ListViewItem(oku["MarkaAdi"].ToString() + " " + oku["UrunAdi"].ToString());
                        ekle.SubItems.Add(oku["SatisFiyati"].ToString());
                        ekle.SubItems.Add(oku["MevcutStok"].ToString());
                        ekle.Tag = oku["UrunID"].ToString();
                        // Listeye ekliyoruz
                        listView1.Items.Add(ekle);
                    }
                }
                oku.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veri çekme hatası: " + ex.Message);
            }
            finally
            {
                baglanti.Close();
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();

            // 2. Bağlantı cümlemiz (Senin kurduğun bağlantı nesnesini kullanıyoruz)
            string query = "SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.SatisFiyati, u.MevcutStok " +
               "FROM Urunler u " +
               "JOIN Markalar m ON u.MarkaID = m.MarkaID " +
               "WHERE u.TurID = 3";

            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                SqlCommand komut = new SqlCommand(query, baglanti);
                SqlDataReader oku = komut.ExecuteReader();

                while (oku.Read())
                {


                    if (Convert.ToInt32(oku["MevcutStok"]) > 0)
                    {
                        ListViewItem ekle = new ListViewItem(oku["MarkaAdi"].ToString() + " " + oku["UrunAdi"].ToString());
                        ekle.SubItems.Add(oku["SatisFiyati"].ToString());
                        ekle.SubItems.Add(oku["MevcutStok"].ToString());
                        ekle.Tag = oku["UrunID"].ToString();
                        // Listeye ekliyoruz
                        listView1.Items.Add(ekle);
                    }
                }
                oku.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veri çekme hatası: " + ex.Message);
            }
            finally
            {
                baglanti.Close();
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();

            // 2. Bağlantı cümlemiz (Senin kurduğun bağlantı nesnesini kullanıyoruz)
            string query = "SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.SatisFiyati, u.MevcutStok " +
               "FROM Urunler u " +
               "JOIN Markalar m ON u.MarkaID = m.MarkaID " +
               "WHERE u.TurID = 4";

            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                SqlCommand komut = new SqlCommand(query, baglanti);
                SqlDataReader oku = komut.ExecuteReader();

                while (oku.Read())
                {


                    if (Convert.ToInt32(oku["MevcutStok"]) > 0)
                    {
                        ListViewItem ekle = new ListViewItem(oku["MarkaAdi"].ToString() + " " + oku["UrunAdi"].ToString());
                        ekle.SubItems.Add(oku["SatisFiyati"].ToString());
                        ekle.SubItems.Add(oku["MevcutStok"].ToString());
                        ekle.Tag = oku["UrunID"].ToString();
                        // Listeye ekliyoruz
                        listView1.Items.Add(ekle);
                    }
                }
                oku.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veri çekme hatası: " + ex.Message);
            }
            finally
            {
                baglanti.Close();
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();

            // 2. Bağlantı cümlemiz (Senin kurduğun bağlantı nesnesini kullanıyoruz)
            string query = "SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.SatisFiyati, u.MevcutStok " +
               "FROM Urunler u " +
               "JOIN Markalar m ON u.MarkaID = m.MarkaID " +
               "WHERE u.TurID = 5";

            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                SqlCommand komut = new SqlCommand(query, baglanti);
                SqlDataReader oku = komut.ExecuteReader();

                while (oku.Read())
                {


                    if (Convert.ToInt32(oku["MevcutStok"]) > 0)
                    {
                        ListViewItem ekle = new ListViewItem(oku["MarkaAdi"].ToString() + " " + oku["UrunAdi"].ToString());
                        ekle.SubItems.Add(oku["SatisFiyati"].ToString());
                        ekle.SubItems.Add(oku["MevcutStok"].ToString());
                        ekle.Tag = oku["UrunID"].ToString();
                        // Listeye ekliyoruz
                        listView1.Items.Add(ekle);
                    }
                }
                oku.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veri çekme hatası: " + ex.Message);
            }
            finally
            {
                baglanti.Close();
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();

            // 2. Bağlantı cümlemiz (Senin kurduğun bağlantı nesnesini kullanıyoruz)
            string query = "SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.SatisFiyati, u.MevcutStok " +
               "FROM Urunler u " +
               "JOIN Markalar m ON u.MarkaID = m.MarkaID " +
               "WHERE u.TurID = 6";

            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                SqlCommand komut = new SqlCommand(query, baglanti);
                SqlDataReader oku = komut.ExecuteReader();

                while (oku.Read())
                {


                    if (Convert.ToInt32(oku["MevcutStok"]) > 0)
                    {
                        ListViewItem ekle = new ListViewItem(oku["MarkaAdi"].ToString() + " " + oku["UrunAdi"].ToString());
                        ekle.SubItems.Add(oku["SatisFiyati"].ToString());
                        ekle.SubItems.Add(oku["MevcutStok"].ToString());
                        ekle.Tag = oku["UrunID"].ToString();
                        // Listeye ekliyoruz
                        listView1.Items.Add(ekle);
                    }
                }
                oku.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veri çekme hatası: " + ex.Message);
            }
            finally
            {
                baglanti.Close();
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();

            // 2. Bağlantı cümlemiz (Senin kurduğun bağlantı nesnesini kullanıyoruz)
            string query = "SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.SatisFiyati, u.MevcutStok " +
               "FROM Urunler u " +
               "JOIN Markalar m ON u.MarkaID = m.MarkaID " +
               "WHERE u.TurID = 7";

            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                SqlCommand komut = new SqlCommand(query, baglanti);
                SqlDataReader oku = komut.ExecuteReader();

                while (oku.Read())
                {


                    if (Convert.ToInt32(oku["MevcutStok"]) > 0)
                    {
                        ListViewItem ekle = new ListViewItem(oku["MarkaAdi"].ToString() + " " + oku["UrunAdi"].ToString());
                        ekle.SubItems.Add(oku["SatisFiyati"].ToString());
                        ekle.SubItems.Add(oku["MevcutStok"].ToString());
                        ekle.Tag = oku["UrunID"].ToString();
                        // Listeye ekliyoruz
                        listView1.Items.Add(ekle);
                    }
                }
                oku.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veri çekme hatası: " + ex.Message);
            }
            finally
            {
                baglanti.Close();
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();

            // 2. Bağlantı cümlemiz (Senin kurduğun bağlantı nesnesini kullanıyoruz)
            string query = "SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.SatisFiyati, u.MevcutStok " +
               "FROM Urunler u " +
               "JOIN Markalar m ON u.MarkaID = m.MarkaID " +
               "WHERE u.TurID = 8";

            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                SqlCommand komut = new SqlCommand(query, baglanti);
                SqlDataReader oku = komut.ExecuteReader();

                while (oku.Read())
                {


                    if (Convert.ToInt32(oku["MevcutStok"]) > 0)
                    {
                        ListViewItem ekle = new ListViewItem(oku["MarkaAdi"].ToString() + " " + oku["UrunAdi"].ToString());
                        ekle.SubItems.Add(oku["SatisFiyati"].ToString());
                        ekle.SubItems.Add(oku["MevcutStok"].ToString());
                        ekle.Tag = oku["UrunID"].ToString();
                        // Listeye ekliyoruz
                        listView1.Items.Add(ekle);
                    }
                }
                oku.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veri çekme hatası: " + ex.Message);
            }
            finally
            {
                baglanti.Close();
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();

            // 2. Bağlantı cümlemiz (Senin kurduğun bağlantı nesnesini kullanıyoruz)
            string query = "SELECT u.UrunID, u.UrunAdi, m.MarkaAdi, u.SatisFiyati, u.MevcutStok " +
               "FROM Urunler u " +
               "JOIN Markalar m ON u.MarkaID = m.MarkaID " +
               "WHERE u.TurID = 9";

            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                SqlCommand komut = new SqlCommand(query, baglanti);
                SqlDataReader oku = komut.ExecuteReader();

                while (oku.Read())
                {


                    if (Convert.ToInt32(oku["MevcutStok"]) > 0)
                    {
                        ListViewItem ekle = new ListViewItem(oku["MarkaAdi"].ToString() + " " + oku["UrunAdi"].ToString());
                        ekle.SubItems.Add(oku["SatisFiyati"].ToString());
                        ekle.SubItems.Add(oku["MevcutStok"].ToString());
                        ekle.Tag = oku["UrunID"].ToString();
                        // Listeye ekliyoruz
                        listView1.Items.Add(ekle);
                    }
                }
                oku.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veri çekme hatası: " + ex.Message);
            }
            finally
            {
                baglanti.Close();
            }
        }

        private void button23_Click(object sender, EventArgs e)
        {
            AYARLAR form2 = new AYARLAR();
            this.Hide();
            form2.ShowDialog();
            this.Show();

            

        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button10_Click(object sender, EventArgs e)
        {
            if (listView2.SelectedItems.Count > 0)
            {
                ListViewItem seciliSepetUrünü = listView2.SelectedItems[0];


                string urunID = seciliSepetUrünü.Tag.ToString();


                int guncelStok = 0;

                baglanti.Open();
                string stokSorgusu = "SELECT MevcutStok FROM Urunler WHERE UrunID = @id";
                using (SqlCommand komut = new SqlCommand(stokSorgusu, baglanti))
                {
                    komut.Parameters.AddWithValue("@id", urunID);
                    guncelStok = Convert.ToInt32(komut.ExecuteScalar());
                }

                int sepettekiAdet = int.Parse(seciliSepetUrünü.SubItems[2].Text);
                decimal uruntoplam = decimal.Parse(seciliSepetUrünü.SubItems[3].Text);
                decimal urunfiyat = decimal.Parse(seciliSepetUrünü.SubItems[1].Text);

                if (sepettekiAdet > 0)
                {
                    sepettekiAdet--;
                    uruntoplam = sepettekiAdet * urunfiyat;
                    seciliSepetUrünü.SubItems[3].Text = uruntoplam.ToString();
                    ToplamHesapla();
                    seciliSepetUrünü.SubItems[2].Text = sepettekiAdet.ToString();

                }
                else
                {
                    MessageBox.Show($"Alt sınıra ulaşıldı ! Eğer ürünü silmek istiyorsanız Sil Butonunu kullanın.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show("Lütfen sepetten bir ürün seçin.");
            }
            baglanti.Close();
        }
    }
}