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
using System.Windows.Forms.DataVisualization.Charting;

namespace per101
{
    public partial class AYARLAR : Form
    {
        SqlConnection baglanti = new SqlConnection(@"Data Source=MertPC\SQLEXPRESS;Initial Catalog=per10Database;User ID=sa;Password=1;Encrypt=True;TrustServerCertificate=True");
        public AYARLAR()
        {
            InitializeComponent();
        }


        // Ana çizim metodumuz
        private void KarGrafigiCiz(string periyotTipi)
        {
            chart3.Series.Clear();

            Series seri = new Series("ZamanBazliKar");
            seri.ChartType = SeriesChartType.Line; // Çizgi Grafik
            seri.BorderWidth = 3; // Çizgi biraz kalın ve tok dursun
            seri.MarkerStyle = MarkerStyle.Circle; // Nokta başlarına yuvarlak koysun
            seri.MarkerSize = 8;
            seri.IsValueShownAsLabel = true;
            seri.LabelFormat = "C2"; // Parasal format (₺)

            chart3.Series.Add(seri);

            // X Ekseni yazıları DİK (-90 derece) ve hepsi görünsün
            chart3.ChartAreas[0].AxisX.LabelStyle.Angle = -90;
            chart3.ChartAreas[0].AxisX.Interval = 1;

            using (SqlConnection baglanti = new SqlConnection("Data Source=MertPC\\SQLEXPRESS;Initial Catalog=per10Database;User ID=sa;Password=1;Encrypt=True;TrustServerCertificate=True"))
            {
                SqlCommand komut = new SqlCommand("ZamanaGoreKarGetir", baglanti);
                komut.CommandType = CommandType.StoredProcedure;

                // İstenilen tarih: 1 Şubat 2026'dan, şu ana kadar
                komut.Parameters.AddWithValue("@basTarihi", new DateTime(2026, 2, 1));
                komut.Parameters.AddWithValue("@bitTarihi", DateTime.Now);
                komut.Parameters.AddWithValue("@Periyot", periyotTipi);

                try
                {
                    baglanti.Open();
                    SqlDataReader dr = komut.ExecuteReader();

                    while (dr.Read())
                    {
                        string donem = dr["Donem"].ToString();
                        decimal kar = Convert.ToDecimal(dr["Kar"]);

                        chart3.Series["ZamanBazliKar"].Points.AddXY(donem, kar);
                    }
                    dr.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Grafik hatası: " + ex.Message);
                }
            }
        }

        public void MarkalariGetir()
        {
            SqlDataAdapter da = new SqlDataAdapter("SELECT MarkaID, MarkaAdi FROM Markalar", baglanti);
            DataTable dt = new DataTable();
            da.Fill(dt);

            comboBox3.DisplayMember = "MarkaAdi"; // Ekranda görünecek sütun [cite: 2026-02-02]
            comboBox3.ValueMember = "MarkaID";    // Arka planda tutulacak ID [cite: 2026-02-05]
            comboBox3.DataSource = dt;            // Veriyi bağla [cite: 2026-02-05]
        }
        public void TürleriGetir1()
        {
            SqlDataAdapter da = new SqlDataAdapter("SELECT TurID, TurAdi FROM Turler", baglanti);
            DataTable dt = new DataTable();
            da.Fill(dt);

            comboBox1.DisplayMember = "TurAdi"; // Ekranda görünecek sütun [cite: 2026-02-02]
            comboBox1.ValueMember = "TurID";    // Arka planda tutulacak ID [cite: 2026-02-05]
            comboBox1.DataSource = dt;

            comboBox2.DisplayMember = "TurAdi"; // Ekranda görünecek sütun [cite: 2026-02-02]
            comboBox2.ValueMember = "TurID";    // Arka planda tutulacak ID [cite: 2026-02-05]
            comboBox2.DataSource = dt;            // Veriyi bağla [cite: 2026-02-05]
        }
        private void TureGoreListele(int turID)
        {
            listView1.Items.Clear();


            string query = "SELECT u.UrunID, u.UrunAdi,u.AlisFiyati, m.MarkaAdi, u.SatisFiyati, u.MevcutStok " +
               "FROM Urunler u " +
               "JOIN Markalar m ON u.MarkaID = m.MarkaID " +
               "WHERE u.TurID = " + turID.ToString();

            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                SqlCommand komut = new SqlCommand(query, baglanti);
                SqlDataReader oku = komut.ExecuteReader();

                while (oku.Read())
                {



                    ListViewItem ekle = new ListViewItem(oku["UrunID"].ToString());

                    ekle.SubItems.Add(oku["MarkaAdi"].ToString() + " " + oku["UrunAdi"].ToString());
                    ekle.SubItems.Add(oku["AlisFiyati"].ToString());
                    ekle.SubItems.Add(oku["SatisFiyati"].ToString());
                    ekle.SubItems.Add(oku["MevcutStok"].ToString());

                    listView1.Items.Add(ekle);

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
        private void SepetDetaylariniGetir(int sepetID)
        {
            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();
                listView3.Items.Clear(); // Sadece detay listesini temizle

                using (SqlCommand cmd = new SqlCommand("sp_SepetDetayiniGetir", baglanti))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@SepetID", sepetID);

                    SqlDataReader oku = cmd.ExecuteReader();
                    while (oku.Read())
                    {
                        // ListView2: Ürün Adı, Miktar, Birim Fiyat
                        ListViewItem ekle = new ListViewItem(oku["MarkaAdi"].ToString() + " " + oku["UrunAdi"].ToString());
                        ekle.SubItems.Add(oku["Miktar"].ToString());
                        ekle.SubItems.Add(Convert.ToDecimal(oku["birimsatisfiyati"]).ToString("N2") + " TL");

                        listView3.Items.Add(ekle);
                    }
                    oku.Close();
                }
            }
            catch (Exception ex) { MessageBox.Show("Detay getirme hatası: " + ex.Message); }
            finally { baglanti.Close(); }
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            MarkalariGetir();
            TürleriGetir1();

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {

        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                if (baglanti.State == ConnectionState.Closed)
                    baglanti.Open();

                listView1.Items.Clear(); // Önce ana listeyi temizle
                listView2.Items.Clear(); // Detay listesini de temizle ki kafa karışmasın

                using (SqlCommand cmd = new SqlCommand("sp_SepetleriFiltrele", baglanti))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // DateTimePicker'dan gelen tarihleri gönderiyoruz
                    cmd.Parameters.AddWithValue("@Baslangic", dateTimePicker1.Value.Date.AddHours(6));
                    cmd.Parameters.AddWithValue("@Bitis", dateTimePicker2.Value.Date.AddDays(1).AddHours(3));

                    SqlDataReader oku = cmd.ExecuteReader();
                    while (oku.Read())
                    {
                        // ListView1: SepetID, Toplam Tutar, Tarih
                        ListViewItem ekle = new ListViewItem(oku["SepetID"].ToString());
                        ekle.SubItems.Add(Convert.ToDecimal(oku["ToplamTutar"]).ToString("N2") + " TL");
                        ekle.SubItems.Add(Convert.ToDateTime(oku["Tarih"]).ToString("g")); // "g" kısa tarih ve saat verir

                        listView2.Items.Add(ekle);
                    }
                    oku.Close();
                }
            }
            catch (Exception ex) { MessageBox.Show("Filtreleme hatası: " + ex.Message); }
            finally { baglanti.Close(); }
        }

        private void listView2_SelectedIndexChanged_1(object sender, EventArgs e)
        {

            if (listView2.SelectedItems.Count > 0)
            {
                int secilenSepetID = Convert.ToInt32(listView2.SelectedItems[0].Text);
                SepetDetaylariniGetir(secilenSepetID);
            }

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int secilenIndeks = comboBox1.SelectedIndex;

            switch (secilenIndeks)
            {
                case 0:

                    TureGoreListele(secilenIndeks + 1);
                    break;
                case 1:

                    TureGoreListele(secilenIndeks + 1);
                    break;
                case 2:

                    TureGoreListele(secilenIndeks + 1);
                    break;
                case 3:

                    TureGoreListele(secilenIndeks + 1);
                    break;
                case 4:

                    TureGoreListele(secilenIndeks + 1);
                    break;
                case 5:

                    TureGoreListele(secilenIndeks + 1);
                    break;
                case 6:

                    TureGoreListele(secilenIndeks + 1);
                    break;
                case 7:

                    TureGoreListele(secilenIndeks + 1);
                    break;
                case 8:

                    TureGoreListele(secilenIndeks + 1);
                    break;
                default:
                    break;
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                try
                {
                    if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                    using (SqlCommand cmd = new SqlCommand("sp_UrunGuncelleDetayli", baglanti))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // ListView'daki gizli ID'yi alıyoruz
                        int urunID = Convert.ToInt32(listView1.SelectedItems[0].Text);


                        // Parametreleri TextBox'lardan çekiyoruz
                        cmd.Parameters.AddWithValue("@UrunID", urunID);
                        cmd.Parameters.AddWithValue("@AlisFiyati", Convert.ToDecimal(alistext.Text));
                        cmd.Parameters.AddWithValue("@SatisFiyati", Convert.ToDecimal(satistext.Text));
                        cmd.Parameters.AddWithValue("@Stok", Convert.ToInt32(stoktext.Text));


                        cmd.ExecuteNonQuery();
                        baglanti.Close();
                        TureGoreListele(comboBox1.SelectedIndex + 1);
                        MessageBox.Show("Ürün bilgileri başarıyla güncellendi!", "Bilgi");
                        alistext.Clear();
                        satistext.Clear();
                        stoktext.Clear();





                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Güncelleme hatası: " + ex.Message);
                }
                finally
                {
                    baglanti.Close();
                }
            }
            else
            {
                MessageBox.Show("Lütfen listeden güncellenecek ürünü seçin!");
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                textBox6.Enabled = true;    // Yeni marka girişi açılır
                comboBox3.Enabled = false;  // Mevcut marka seçimi kapanır
            }
            else
            {
                textBox6.Enabled = false;   // Yeni marka girişi kapanır
                comboBox3.Enabled = true;   // Mevcut marka seçimi açılır
            }
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            try
            {
                if (baglanti.State == ConnectionState.Closed) baglanti.Open();

                using (SqlCommand cmd = new SqlCommand("sp_AkilliUrunEkle", baglanti))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Marka Seçimi Mantığı
                    if (checkBox1.Checked)
                    {
                        cmd.Parameters.AddWithValue("@YeniMarkaAdi", textBox6.Text.Trim());
                        cmd.Parameters.AddWithValue("@MevcutMarkaID", 0);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@YeniMarkaAdi", DBNull.Value);
                        // ComboBox'ın ValueMember özelliğinde MarkaID tuttuğunu varsayıyorum
                        int marka = Convert.ToInt32(comboBox3.SelectedValue);
                        cmd.Parameters.AddWithValue("@MevcutMarkaID", marka);
                    }

                    int tür = Convert.ToInt32(comboBox2.SelectedValue);
                    cmd.Parameters.AddWithValue("@TurID", tür);
                    cmd.Parameters.AddWithValue("@UrunAdi", textBox5.Text);
                    cmd.Parameters.AddWithValue("@AlisFiyati", Convert.ToDecimal(textBox4.Text));
                    cmd.Parameters.AddWithValue("@SatisFiyati", Convert.ToDecimal(textBox3.Text));
                    cmd.Parameters.AddWithValue("@Stok", Convert.ToInt32(textBox2.Text));
                    cmd.Parameters.AddWithValue("@KritikStok", Convert.ToInt32(textBox1.Text));

                    cmd.ExecuteNonQuery();
                    MessageBox.Show("Ürün başarıyla kaydedildi!", "Sistem Bilgisi");


                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kayıt Hatası: " + ex.Message);
            }
            finally { baglanti.Close(); }
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button5_Click(object sender, EventArgs e)
        {


            SqlCommand komut = new SqlCommand("SatisAnalizGetir", baglanti);
            komut.CommandType = CommandType.StoredProcedure;


            komut.Parameters.AddWithValue("@baslangic", dateTimePicker1.Value.Date);


            komut.Parameters.AddWithValue("@bitis", dateTimePicker2.Value.Date.AddDays(1).AddSeconds(-1));

            try
            {
                baglanti.Open();
                SqlDataReader dr = komut.ExecuteReader();

                if (dr.Read())
                {

                    decimal ciro = Convert.ToDecimal(dr["ToplamCiro"]);
                    label19.Text = ciro.ToString("C2");


                    decimal kar = Convert.ToDecimal(dr["ToplamKar"]);
                    label20.Text = kar.ToString("C2");

                    label22.Text = "KDV Düşülmemiştir !";
                    if (kar < 0) label20.ForeColor = Color.Red;
                    else label20.ForeColor = Color.Green;
                }
                dr.Close();
                baglanti.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hesap hatası reis: " + ex.Message);
            }
        }





        private void chart1_Click(object sender, EventArgs e)
        {

        }


        private void dateTimePicker3_ValueChanged(object sender, EventArgs e)
        {

        }

        private void dateTimePicker4_ValueChanged(object sender, EventArgs e)
        {

        }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {

        }

        private void dateTimePicker2_ValueChanged(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            chart1.Series.Clear();

            Series seri = new Series("En Çok Satanlar");
            seri.ChartType = SeriesChartType.Bar; 
            seri.IsValueShownAsLabel = true; 

            chart1.ChartAreas[0].AxisX.Interval = 1;
            chart1.ChartAreas[0].AxisX.LabelStyle.Angle = -90;
            chart1.ChartAreas[0].AxisX.IsMarginVisible = false;
            chart1.Series.Add(seri);
            chart1.ChartAreas[0].AxisX.IsReversed = true;


            using (SqlConnection baglanti = new SqlConnection(@"Data Source=MertPC\SQLEXPRESS;Initial Catalog=per10Database;User ID=sa;Password=1;Encrypt=True;TrustServerCertificate=True"))
            {

                SqlCommand komut = new SqlCommand("ilk5getir_procedure", baglanti);


                komut.CommandType = CommandType.StoredProcedure;


                komut.Parameters.AddWithValue("@basTarihi", dateTimePicker3.Value.Date);
                komut.Parameters.AddWithValue("@bitTarihi", dateTimePicker4.Value.Date.AddDays(1).AddSeconds(-1));

                try
                {
                    baglanti.Open();
                    SqlDataReader dr = komut.ExecuteReader();

                    while (dr.Read())
                    {

                        string urunIsmi = dr[0].ToString(); // İlk kolon (UrunAdi)
                        int satisMiktari = Convert.ToInt32(dr[1]);


                        chart1.Series["En Çok Satanlar"].Points.AddXY(urunIsmi, satisMiktari);
                    }
                    dr.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Bir hata oldu reis: " + ex.Message);
                }
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            
            chart2.Series.Clear();

            Series seri = new Series("En Çok Kâr Bırakanlar");
            seri.ChartType = SeriesChartType.Bar;
            seri.IsValueShownAsLabel = true; 

         
            seri.LabelFormat = "C2";

            chart2.Series.Add(seri);

            // Grafik Makyajı
            chart2.ChartAreas[0].AxisX.Interval = 1;
            chart2.ChartAreas[0].AxisX.LabelStyle.Angle = -90;
            chart2.ChartAreas[0].AxisX.IsMarginVisible = false;
            chart2.ChartAreas[0].AxisX.IsReversed = true;

            using (SqlConnection baglanti = new SqlConnection(@"Data Source=MertPC\SQLEXPRESS;Initial Catalog=per10Database;User ID=sa;Password=1;Encrypt=True;TrustServerCertificate=True"))
            {
               
                SqlCommand komut = new SqlCommand("EnCokKarGetirenler", baglanti);
                komut.CommandType = CommandType.StoredProcedure;

                komut.Parameters.AddWithValue("@basTarihi", dateTimePicker3.Value.Date);
                komut.Parameters.AddWithValue("@bitTarihi", dateTimePicker4.Value.Date.AddDays(1).AddSeconds(-1));

                try
                {
                    baglanti.Open();
                    SqlDataReader dr = komut.ExecuteReader();

                    while (dr.Read())
                    {
                        string urunIsmi = dr[0].ToString();

                        // DİKKAT: Artık Adet (int) değil, Para (decimal) okuyoruz
                        decimal toplamKar = Convert.ToDecimal(dr[1]);

                        // Grafiğe ekle (X: İsim, Y: Tutar)
                        // Bar grafiği olduğu için Chart bunu otomatik yatay dizecek
                        chart2.Series["En Çok Kâr Bırakanlar"].Points.AddXY(urunIsmi, toplamKar);
                    }
                    dr.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Bir hata oldu reis: " + ex.Message);
                }
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            KarGrafigiCiz("GUN");
        }

        private void button8_Click(object sender, EventArgs e)
        {
            KarGrafigiCiz("HAFTA");
        }

        private void button9_Click(object sender, EventArgs e)
        {
            KarGrafigiCiz("AY");
        }
    }
}

