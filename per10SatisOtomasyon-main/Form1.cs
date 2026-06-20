using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace per101
{
    public partial class giris_form : Form
    {
        public giris_form()
        {
            InitializeComponent();
            this.AcceptButton = giris_buton;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }


        private void pictureBox2_Click(object sender, EventArgs e)
        {

        }

        

        private void cikis_buton_Click(object sender, EventArgs e)
        {
           this.Close();
        }
        public string id = "peronyıkama";
        public string sifre = "per1023";

        private async void giris_buton_Click(object sender, EventArgs e)
        {
            if (id == textBox1.Text && sifre == textBox2.Text)
            {
                label3.Text = null;
                label4.Text = "Giriş Ypılıyor...";
                await Task.Delay(1500);
                ÜRÜNLER yeni = new ÜRÜNLER();
                yeni.Show();
                this.Hide();
            }
            else
            {
                label3.Text = "Hatalı kullanıcı adı veya şifre!";
                textBox1.Clear();
                textBox2.Clear();
                textBox1.Focus();
               
            }
           
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
