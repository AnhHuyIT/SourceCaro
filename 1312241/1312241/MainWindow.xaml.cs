using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json.Linq;
using Quobject.SocketIoClientDotNet.Client;
using System.Threading;
using _1312241.Chess;
using System.ComponentModel;


namespace _1312241
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public enum OptionPlayer
    {
        Choose,
        Human,
        Com,
        Online,
    }

    public struct node
    {
        public int row, column;
        public node(int _row, int _column)
        {
            this.row = _row;
            this.column = _column;
        }
    }
    public partial class MainWindow : Window
    {
        

        private int row, column;
        private const int length = 35;
        public int luotchoi = 1; // lượt chơi của người chơi, = 1 đánh X, = 2 đánh O
        public int[,] isCell;

        public OptionPlayer currOption;
        public node ChessOnline;
        private bool Newgame = true;
        private string NamePlayer;
       
        //AI
        public int[,] Cost;
        public int[] ComAttack = new int[5] { 0, 2, 45, 1000, 5000 };
        public int[] HumanAttack = new int[5] { 0, 1, 25, 200, 4500 };
        public int numHuman, numCom;
        private node Point_AI;
        private readonly BackgroundWorker worker = new BackgroundWorker();

        public Socket socket;
        
        public MainWindow()
        {
            this.InitializeComponent();


           // Chess = new CBanCo(this, BanCo, socket);
            row = column = 12;
            isCell = new int[row, column];
            Cost = new int[row, column];
            ResetCost();
            SetCell();
            BanCo.MouseDown += new System.Windows.Input.MouseButtonEventHandler(BanCo_MouseDown);
            VeBanCo();
            currOption = OptionPlayer.Choose;
            Comp.IsEnabled = false;
            Hum.IsEnabled = false;
            Internet.IsEnabled = false;
            worker.DoWork += find_location;
            worker.RunWorkerCompleted += AI_play;
           
        }
        private void AI_play(object sender, RunWorkerCompletedEventArgs e) //Máy đánh
        {
            isCell[Point_AI.row, Point_AI.column] = 2;
            DrawO(Point_AI.row, Point_AI.column);
            int end = EndGame(Point_AI.row, Point_AI.column);
            if ((end == 1) && (luotchoi == 2))
            {
                currOption = OptionPlayer.Choose;
                MessageBox.Show("Computer đã giành chiến thắng");
                Newgame = false;
            }
            luotchoi = 1;
        }
        private void find_location(object sender, DoWorkEventArgs e)  //Tìm vị trí cho máy đánh
        {
            Thread.Sleep(500);
            ResetCost();
            Evaluate();
            node temp = Getnodemax();
            Point_AI.row = (int)temp.row;
            Point_AI.column = (int)temp.column;
        } 

        #region Sự kiện trên bàn cờ
        public void BanCo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            System.GC.Collect();
            int end = 0;
            Point Post = e.GetPosition(BanCo); // Lấy thông tin tọa độ

            //Xử lý tọa độ
            int x = (int)Post.X / length;
            int y = (int)Post.Y / length;

            switch (currOption)
            {
                case OptionPlayer.Choose:
                    if (Newgame == true)
                        MessageBox.Show("Bạn chưa chọn kiểu chơi!!!");
                    else
                        MessageBox.Show("Ván đấu đã kết thúc. Mời bạn chọn New Game để tiếp tục");
                    break;
                case OptionPlayer.Human:
                    #region HumvsHum



                    // người với người
                    if (isCell[x, y] == 0)
                    {
                        if (luotchoi == 1)
                        {
                            isCell[x, y] = 1;
                            DrawX(x, y);
                            luotchoi = 2;
                        }
                        else
                        {
                            isCell[x, y] = 2;
                            DrawO(x, y);
                            luotchoi = 1;
                        }
                    }

                    end = EndGame(x, y);
                    if (end == 1)
                    {
                        if (luotchoi == 1)
                            MessageBox.Show("Player 2 là người chiến thắng!!!");
                        else
                            MessageBox.Show("Player 1 là người chiến thắng!!!");
                        currOption = OptionPlayer.Choose;
                        Newgame = false;
                    }

                    break;
                    #endregion
                case OptionPlayer.Com:
                    #region HumvsCom

                    // người với máy
                    if (isCell[x, y] == 0)
                    {
                        if ((luotchoi == 1) && (end == 0))
                        {
                            isCell[x, y] = 1;
                            DrawX(x, y);
                            end = EndGame(x, y);
                            luotchoi = 2;
                            if (end == 1)
                            {
                                luotchoi = 1;
                                MessageBox.Show("Human đã giành chiến thắng");
                               
                                currOption = OptionPlayer.Choose;
                                Newgame = false;
                            }
                            else
                                worker.RunWorkerAsync();
                        }

                        //if ((luotchoi == 2) && (end == 0))
                        //{
                        //    ResetCost();
                        //    Evaluate();


                        //    node n = Getnodemax();
                        //    isCell[n.row, n.column] = 2;
                        //    DrawO(n.row, n.column);
                        //    luotchoi = 1;
                        //    end = EndGame(n.row, n.column);
                        //    if (end == 1)
                        //    {
                        //        MessageBox.Show("Computer đã giành chiến thắng");
                        //        currOption = OptionPlayer.Choose;
                        //        Newgame = false;
                        //    }

                        //}
                    }
                    #endregion
                    break;
                case OptionPlayer.Online:

                    if (luotchoi == 1)
                    {
                        DrawX(x, y);
                        socket.Emit("MyStepIs", JObject.FromObject(new { row = y, col = x }));
                        luotchoi = 2;
                    }

                    break;
            }
        }
        #endregion

        #region AI
        public void ResetCost()
        {
            for (int i = 0; i < row; i++)
                for (int j = 0; j < column; j++)
                    Cost[i, j] = 0;
        }
        public void Evaluate()
        {
            #region  //Lượng giá theo hàng
            {
                for (int i = 0; i < row; i++)
                    for (int j = 0; j < column - 4; j++)
                    {
                        numCom = numHuman = 0;
                        for (int k = 0; k <= 4; k++)
                        {
                            if (isCell[i, j + k] == 1) numHuman++;
                            if (isCell[i, j + k] == 2) numCom++;

                        }
                        if (numHuman * numCom == 0 && numCom != numHuman) //5 ô chỉ có 1 loại cờ Human hoặc Com
                        {
                            if (numCom != 0)  //chỉ toàn là cờ của Com
                            {
                                for (int k = 0; k <= 4; k++)
                                {
                                    if (isCell[i, j + k] == 0)
                                    {
                                        Cost[i, j + k] += ComAttack[numCom];
                                        if (numCom == 4) Cost[i, j + k] *= 10;
                                        if (j - 1 >= 0 && j + 5 <= column - 1 && ((isCell[i, j - 1] == 1 && isCell[i, j + 5] == 1)))
                                        {
                                            Cost[i, j + k] = 0;
                                        }
                                        if (numCom == 4 && j - 1 >= 0 && j + 5 <= column - 1 && ((isCell[i, j - 1] == 2 || isCell[i, j + 5] == 2)))
                                        {
                                            Cost[i, j + k] = 0;
                                        }
                                    }
                                }
                            }
                            else   //chỉ toàn là cờ của Human
                            {

                                for (int k = 0; k <= 4; k++)

                                    if (isCell[i, j + k] == 0)
                                    {


                                        Cost[i, j + k] += HumanAttack[numHuman];
                                        if (numHuman == 4) Cost[i, j + k] *= 10;
                                        if (j - 1 >= 0 && j + 5 <= column - 1 && ((isCell[i, j - 1] == 2 && isCell[i, j + 5] == 2)))
                                        {
                                            Cost[i, j + k] = 0;
                                        }
                                        if (numHuman == 4 && j - 1 >= 0 && j + 5 <= column - 1 && ((isCell[i, j - 1] == 1 || isCell[i, j + 5] == 1)))
                                        {
                                            Cost[i, j + k] = 0;
                                        }

                                    }

                            }


                        }

                    }

            }
            #endregion
            #region  //Lượng giá theo cột
            {
                for (int i = 0; i < row - 4; i++)
                    for (int j = 0; j < column; j++)
                    {
                        numCom = numHuman = 0;
                        for (int k = 0; k <= 4; k++)
                        {
                            if (isCell[i + k, j] == 1) numHuman++;
                            if (isCell[i + k, j] == 2) numCom++;

                        }
                        if (numHuman * numCom == 0 && numCom != numHuman) //5 ô chỉ có 1 loại cờ Human hoặc Com
                        {
                            if (numCom != 0)  //chỉ toàn là cờ của Com
                            {
                                for (int k = 0; k <= 4; k++)
                                {
                                    if (isCell[i + k, j] == 0)
                                    {
                                        Cost[i + k, j] += ComAttack[numCom];
                                        if (numCom == 4) Cost[i + k, j] *= 10;
                                        if ((i - 1) >= 0 && (i + 5) <= row - 1 && ((isCell[i - 1, j] == 1 && isCell[i + 5, j] == 1)))
                                        {
                                            if (Cost[i + k, j] > 80000) { } else Cost[i + k, j] = 0;
                                        }
                                        if (numCom == 4 && (i - 1) >= 0 && (i + 5) <= row - 1 && ((isCell[i - 1, j] == 2 || isCell[i + 5, j] == 2)))
                                        {
                                            Cost[i + k, j] = 0;
                                        }
                                    }
                                }
                            }
                            else   //chỉ toàn là cờ của Human
                            {

                                for (int k = 0; k <= 4; k++)
                                {
                                    if (isCell[i + k, j] == 0)
                                    {

                                        Cost[i + k, j] += HumanAttack[numHuman];
                                        if (numHuman == 4) Cost[i + k, j] *= 10;
                                        if ((i - 1) >= 0 && (i + 5) <= row - 1 && ((isCell[i - 1, j] == 2 && isCell[i + 5, j] == 2)))
                                        {
                                            if (Cost[i + k, j] > 80000) { } else Cost[i + k, j] = 0;
                                        }
                                        if (numHuman == 4 && (i - 1) >= 0 && (i + 5) <= row - 1 && ((isCell[i - 1, j] == 1 || isCell[i + 5, j] == 1)))
                                        {
                                            Cost[i + k, j] = 0;
                                        }
                                    }
                                }
                            }


                        }

                    }
            }
            #endregion
            #region //Lượng giá theo đường chéo chính (\)
            {
                for (int i = 0; i < row - 4; i++)
                    for (int j = 0; j < column - 4; j++)
                    {
                        numCom = numHuman = 0;
                        for (int k = 0; k <= 4; k++)
                        {
                            if (isCell[i + k, j + k] == 1) numHuman++;
                            if (isCell[i + k, j + k] == 2) numCom++;

                        }
                        if (numHuman * numCom == 0 && numCom != numHuman) //5 ô chỉ có 1 loại cờ Human hoặc Com
                        {
                            if (numCom != 0)  //chỉ toàn là cờ của Com
                            {
                                for (int k = 0; k <= 4; k++)
                                {
                                    if (isCell[i + k, j + k] == 0)
                                    {
                                        Cost[i + k, j + k] += ComAttack[numCom];
                                        if (numCom == 4) Cost[i + k, j + k] *= 10;
                                        if (i - 1 >= 0 && j - 1 >= 0 && i + 5 <= row - 1 && j + 5 <= column - 1 && ((isCell[i - 1, j - 1] == 1 && isCell[i + 5, j + 5] == 1)))
                                        {
                                            if (Cost[i + k, j + k] > 80000) { } else Cost[i + k, j + k] = 0;
                                        }
                                        if (numCom == 4 && i - 1 >= 0 && j - 1 >= 0 && i + 5 <= row - 1 && j + 5 <= column - 1 && ((isCell[i - 1, j - 1] == 2 || isCell[i + 5, j + 5] == 2)))
                                            Cost[i + k, j + k] = 0;
                                    }
                                }

                            }
                            else   //chỉ toàn là cờ của Human
                            {

                                for (int k = 0; k <= 4; k++)
                                {
                                    if (isCell[i + k, j + k] == 0)
                                    {

                                        Cost[i + k, j + k] += HumanAttack[numHuman];
                                        if (numHuman == 4) Cost[i + k, j + k] *= 10;
                                        if (i - 1 >= 0 && j - 1 >= 0 && i + 5 <= row - 1 && j + 5 <= column - 1 && ((isCell[i - 1, j - 1] == 2 && isCell[i + 5, j + 5] == 2)))
                                        { if (Cost[i + k, j + k] > 80000) { } else Cost[i + k, j + k] = 0; }
                                        if (numHuman == 4 && i - 1 >= 0 && j - 1 >= 0 && i + 5 <= row - 1 && j + 5 <= column - 1 && ((isCell[i - 1, j - 1] == 1 || isCell[i + 5, j + 5] == 1)))
                                            Cost[i + k, j + k] = 0;


                                    }
                                }
                            }
                        }
                    }
            }



            #endregion
            #region //Lượng giá theo đường chéo phụ (/)

            for (int i = column - 1; i >= 4; i--)
                for (int j = 0; j <= row - 5; j++)
                {
                    numCom = numHuman = 0;
                    for (int k = 0; k <= 4; k++)
                    {
                        if (isCell[i - k, j + k] == 1) numHuman++;
                        if (isCell[i - k, j + k] == 2) numCom++;

                    }
                    if (numHuman * numCom == 0 && numCom != numHuman) //5 ô chỉ có 1 loại cờ Human hoặc Com
                    {
                        if (numCom != 0)  //chỉ toàn là cờ của Com
                        {
                            for (int k = 0; k <= 4; k++)
                            {
                                if (isCell[i - k, j + k] == 0)
                                {
                                    Cost[i - k, j + k] += ComAttack[numCom];
                                    if (numCom == 4) Cost[i - k, j + k] *= 10;
                                    if (i >= 6 && i <= 10 && j > 0 && j <= 5 && isCell[i + 1, j - 1] == 1 && isCell[i - 5, j + 5] == 1)
                                    { if (Cost[i - k, j + k] > 80000) { } else Cost[i - k, j + k] = 0; }
                                    if (numCom == 4 && i >= 6 && i <= 10 && j > 0 && j <= 5 && (isCell[i + 1, j - 1] == 2 || isCell[i - 5, j + 5] == 2))
                                        Cost[i - k, j + k] = 0;

                                }
                            }

                        }
                        else   //chỉ toàn là cờ của Human
                        {

                            for (int k = 0; k <= 4; k++)
                            {
                                if (isCell[i - k, j + k] == 0)
                                {

                                    Cost[i - k, j + k] += HumanAttack[numHuman];
                                    if (numHuman == 4) Cost[i - k, j + k] *= 10;
                                    if (i >= 6 && i <= 10 && j > 0 && j <= 5 && isCell[i + 1, j - 1] == 2 && isCell[i - 5, j + 5] == 2)
                                    { if (Cost[i - k, j + k] > 80000) { } else Cost[i - k, j + k] = 0; }
                                    if (numHuman == 4 && i >= 6 && i <= 10 && j > 0 && j <= 5 && (isCell[i + 1, j - 1] == 1 || isCell[i - 5, j + 5] == 1))
                                        Cost[i - k, j + k] = 0;


                                }
                            }
                        }


                    }

                }
            #endregion
        }

        public  node Getnodemax()
        {
            node n = new node();
            int maxCost = Cost[0, 0];
            node[] arrMaxCost = new node[255];
            for (int i = 0; i < 255; i++)
            {
                arrMaxCost[i] = new node();
            }

            int coutMax = 0;
            for (int i = 0; i < row; i++)
                for (int j = 0; j < column; j++)
                {
                    // if (frmmain.rdbHard.IsChecked == true)
                    //{
                    if (Cost[i, j] >= 100 && Cost[i, j] <= 155)
                    {
                        //ngang va doc
                        if (i <= 8 && i >= 3 && j >= 3 && j <= 8 && ((isCell[i, j - 3] + isCell[i, j - 2] + isCell[i, j - 1] + isCell[i, j] + isCell[i, j + 1] + isCell[i, j + 2] + isCell[i, j + 3] == 2) && (isCell[i - 3, j] + isCell[i - 1, j] + isCell[i - 1, j] + isCell[i + 1, j] + isCell[i + 2, j] + isCell[i + 3, j] == 2)))
                            Cost[i, j] = 400;
                        //cheo chinh va cheo phu
                        if (i <= 8 && i >= 3 && j >= 3 && j <= 8 && ((isCell[i - 3, j - 3] + isCell[i - 2, j - 2] + isCell[i - 2, j - 1] + isCell[i, j] + isCell[i + 1, j + 1] + isCell[i + 2, j + 2] + isCell[i + 3, j + 3] == 2) && (isCell[i - 3, j + 3] + isCell[i - 2, j + 2] + isCell[i - 1, j + 1] + isCell[i + 1, j - 1] + isCell[i + 2, j - 2] + isCell[i + 3, j - 3] == 2)))
                            Cost[i, j] = 400;
                        //cheo chinh va ngang
                        if (i <= 8 && i >= 3 && j >= 3 && j <= 8 && ((isCell[i - 3, j - 3] + isCell[i - 2, j - 2] + isCell[i - 2, j - 1] + isCell[i, j] + isCell[i + 1, j + 1] + isCell[i + 2, j + 2] + isCell[i + 3, j + 3] == 2) && (isCell[i, j - 3] + isCell[i, j - 2] + isCell[i, j - 1] + isCell[i, j + 1] + isCell[i, j + 2] + isCell[i, j + 3] == 2)))
                            Cost[i, j] = 400;
                        //cheo chinh va doc
                        if (i <= 8 && i >= 3 && j >= 3 && j <= 8 && ((isCell[i - 3, j - 3] + isCell[i - 2, j - 2] + isCell[i - 2, j - 1] + isCell[i, j] + isCell[i + 1, j + 1] + isCell[i + 2, j + 2] + isCell[i + 3, j + 3] == 2) && (isCell[i - 3, j] + isCell[i - 1, j] + isCell[i - 1, j] + isCell[i + 1, j] + isCell[i + 2, j] + isCell[i + 3, j] == 2)))
                            Cost[i, j] = 400;
                        //cheo phu  va ngang
                        if (i <= 8 && i >= 3 && j >= 3 && j <= 8 && ((isCell[i, j - 3] + isCell[i, j - 2] + isCell[i, j - 1] + isCell[i, j] + isCell[i, j + 1] + isCell[i, j + 2] + isCell[i, j + 3] == 2) && (isCell[i - 3, j + 3] + isCell[i - 2, j + 2] + isCell[i - 1, j + 1] + isCell[i + 1, j - 1] + isCell[i + 2, j - 2] + isCell[i + 3, j - 3] == 2)))
                            Cost[i, j] = 400;
                        //cheo phu va doc
                        if (i <= 8 && i >= 3 && j >= 3 && j <= 8 && (isCell[i - 3, j] + isCell[i - 1, j] + isCell[i - 1, j] + isCell[i, j] + isCell[i + 1, j] + isCell[i + 2, j] + isCell[i + 3, j] == 2) && (isCell[i - 3, j + 3] + isCell[i - 2, j + 2] + isCell[i - 1, j + 1] + isCell[i + 1, j - 1] + isCell[i + 2, j - 2] + isCell[i + 3, j - 3] == 2))
                            Cost[i, j] = 400;



                    }
                    //   }

                    if (Cost[i, j] > maxCost)
                    {
                        n.row = i;
                        n.column = j;
                        maxCost = Cost[i, j];

                    }
                }
            for (int i = 0; i < row; i++)
                for (int j = 0; j < column; j++)
                {
                    if (Cost[i, j] == maxCost)
                    {
                        n.row = i;
                        n.column = j;
                        arrMaxCost[coutMax] = n;
                        coutMax++;
                    }
                }
            Random ran = new Random();
            int choice = ran.Next(coutMax);
            return arrMaxCost[choice];
        }
        #endregion

        #region xử lý kết thúc game
        // Kiểm tra hàng dọc
        public int Kt_Doc(int x, int y)
        {
            int N = 1;
            int temp = isCell[x, y];
            int b = y;
            while (((y - 1) >= 0) && (isCell[x, y - 1] == temp))
            {
                N++;
                y--;
            }

            y = b;

            while (((y + 1) < column) && (isCell[x, y + 1] == temp))
            {
                N++;
                y++;
            }
            if (N >= 5)
                return 1;
            else return 0;
        }

        // Kiểm tra hàng ngang
        public int Kt_Ngang(int x, int y)
        {
            int N = 1;
            int temp = isCell[x, y];
            int a = x;
            while (((x - 1) >= 0) && (isCell[x - 1, y] == temp))
            {
                N++;
                x--;
            }
            x = a;
            while (((x + 1) < row) && (isCell[x + 1, y] == temp))
            {
                N++;
                x++;
            }

            if (N >= 5)
                return 1;
            else return 0;
        }

        // Kiểm tra đường chéo 1
        public int Kt_Cheo1(int x, int y)
        {
            int N = 1;
            int temp = isCell[x, y];
            int a = x;
            int b = y;
            while (((x - 1) >= 0) && ((y - 1) >= 0) && (isCell[x - 1, y - 1] == temp))
            {
                N++;
                x--;
                y--;
            }

            x = a;
            y = b;

            while (((x + 1) < row) && ((y + 1) < column) && (isCell[x + 1, y + 1] == temp))
            {
                N++;
                x++;
                y++;
            }

            if (N >= 5)
                return 1;
            else return 0;
        }

        // Kiểm tra đường chéo 2
        public int Kt_Cheo2(int x, int y)
        {
            int N = 1;
            int temp = isCell[x, y];
            int a = x;
            int b = y;
            while (((x - 1) >= 0) && ((y + 1) < column) && (isCell[x - 1, y + 1] == temp))
            {
                N++;
                x--;
                y++;
            }

            x = a;
            y = b;

            while (((x + 1) < row) && ((y - 1) >= 0) && (isCell[x + 1, y - 1] == temp))
            {
                N++;
                x++;
                y--;
            }

            if (N >= 5)
                return 1;
            else return 0;
        }

        // kiểm tra kết thúc game
        public int EndGame(int x, int y)
        {

            if ((Kt_Ngang(x, y) == 1) || (Kt_Doc(x, y) == 1) || (Kt_Cheo1(x, y) == 1) || (Kt_Cheo2(x, y) == 1))
                return 1;
            else return 0;
        }
        #endregion

        #region vẽ cờ
        public void DrawO(int x, int y)
        {
            UserControl chess = new UserControl_Chess_O1();
            chess.Height = length;
            chess.Width = length;
            chess.HorizontalAlignment = 0;
            chess.VerticalAlignment = 0;
            chess.Margin = new Thickness(x * length, y * length, 0, 0);
            BanCo.Children.Add(chess);
        }

        public void DrawX(int x, int y)
        {
            UserControl chess = new UserControl_Chess_X1();
            chess.Height = length;
            chess.Width = length;
            chess.HorizontalAlignment = 0;
            chess.VerticalAlignment = 0;
            chess.Margin = new Thickness(x * length, y * length, 0, 0);
            BanCo.Children.Add(chess);
        }
        #endregion

        #region Vẽ bàn cờ
        public void SetCell()
        {
            for (int i = 0; i < row; i++)
                for (int j = 0; j < column; j++)
                {
                    isCell[i, j] = 0;
                }
        }
        public void VeBanCo()
        {
            //ve hang
            for (int i = 0; i < row + 1; i++)
            {
                Line ln = new Line();
                ln.Stroke = Brushes.Blue;
                ln.X1 = 0;
                ln.Y1 = i * length;
                ln.X2 = length * row;
                ln.Y2 = i * length;
                ln.HorizontalAlignment = HorizontalAlignment.Left;
                ln.VerticalAlignment = VerticalAlignment.Top;
                BanCo.Children.Add(ln);
            }
            //ve cot;
            for (int i = 0; i < column + 1; i++)
            {
                Line ln = new Line();
                ln.Stroke = Brushes.Blue;
                ln.X1 = i * length;
                ln.Y1 = 0;
                ln.X2 = i * length;
                ln.Y2 = length * column;
                ln.HorizontalAlignment = HorizontalAlignment.Left;
                ln.VerticalAlignment = VerticalAlignment.Top;
                BanCo.Children.Add(ln);
            }
        }
        #endregion

        public void NewGame()
        {
            //BanCo.IsEnabled = true;
            BanCo.Children.Clear();
            this.VeBanCo();
            SetCell();
            luotchoi = 1;
            Point_AI = new node();
            Newgame = true;
        }

        #region Sự kiện click
        private void Send_Text_Click(object sender, RoutedEventArgs e)
        {
            
            if ((Text_Chat.Text != "") && (currOption == OptionPlayer.Online))
            {
                var today = DateTime.Now;
                var dur = new TimeSpan(0, 0, 0, 0);
                DateTime newday = today.Add(dur);
                string str = Convert.ToString(newday);
                //Update_ListChat(Text_Chat.Text, str);
                socket.Emit("ChatMessage", Text_Chat.Text);
                Text_Chat.Text = string.Empty;
                
             }
        }

        string  SetTime()
        {
            var today = DateTime.Now;
            var dur = new TimeSpan(0, 0, 0, 0);
            DateTime newday = today.Add(dur);
            string str = Convert.ToString(newday);
            return str;
        }
        void Update_ListChat(string Text, string str)
        {
            string Chat = "YOU: " + Text + " (" + str + ") ";
            List_Chat.Items.Add(Chat);
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            NewGame();
            Comp.IsEnabled = true;
            Hum.IsEnabled = true;
            Internet.IsEnabled = true;
        }

        private void Hum_Click(object sender, RoutedEventArgs e)
        {
            
            Comp.IsEnabled = false;
            Internet.IsEnabled = false;
            currOption = OptionPlayer.Human;
        }

        private void Comp_Click(object sender, RoutedEventArgs e)
        {
            
            Hum.IsEnabled = false;
            Internet.IsEnabled = false;
            currOption = OptionPlayer.Com;

        }

        
        private void Internet_Click(object sender, RoutedEventArgs e)
        {
            
            Comp.IsEnabled = false;
            Hum.IsEnabled = false;
            luotchoi = 2;
            currOption = OptionPlayer.Online;
            NamePlayer = Text_user.Text;
            socket = IO.Socket("ws://gomoku-lajosveres.rhcloud.com:8000");
            PlayOnline(NamePlayer);
            //PlayOnline();
         }
        #endregion
        public void PlayOnline(string name)
        {
            string str;
            
            socket.On(Socket.EVENT_CONNECT, () =>
            {
                this.Dispatcher.Invoke((Action)(() =>
                {
                    string Time = SetTime();
                    List_Chat.Items.Add("Server: Connect!!! \n" + Time);
                }));
            });

            socket.On(Socket.EVENT_MESSAGE, (data) =>
            {

                this.Dispatcher.Invoke((Action)(() =>
                {
                    str = ((Newtonsoft.Json.Linq.JObject)data)["message"].ToString();
                    List_Chat.Items.Add(str);
                }));
            });
            socket.On(Socket.EVENT_CONNECT_ERROR, (data) =>
            {
                str = ((Newtonsoft.Json.Linq.JObject)data)["message"].ToString();
                this.Dispatcher.Invoke((Action)(() =>
                {
                    List_Chat.Items.Add(str);
                }));
            });
            socket.On("ChatMessage", (data) =>
            {

                if (((Newtonsoft.Json.Linq.JObject)data)["message"].ToString() == "Welcome!")
                {
                    this.Dispatcher.Invoke((Action)(() =>
                    {
                        socket.Emit("MyNameIs", name);
                        socket.Emit("ConnectToOtherPlayer");
                    }));
                }
                this.Dispatcher.Invoke((Action)(() =>
                {
                    str = ((Newtonsoft.Json.Linq.JObject)data)["message"].ToString();
                    var o = JObject.Parse(data.ToString());
                    string TimeSend = SetTime();
                    string strUser = null;
                    if ((string)o["from"] != null)
                    {
                        strUser = (string)o["from"];
                    }
                    else
                    {
                        strUser = "Server";
                    }
                    if (str.IndexOf("<br />") != -1)
                    {
                        str = str.Replace("<br />", " \n        ");
                        if (str.IndexOf("first") != -1)
                        {

                            if (RB_Computer.IsChecked == true)
                            {
                                isCell[5, 5] = 2;
                                DrawX(5, 5);
                                socket.Emit("MyStepIs", JObject.FromObject(new { row = 5, col = 5 }));
                            }
                            else
                                luotchoi = 1;
                        }
                    }

                    List_Chat.Items.Add(strUser + ": " + str + "\n" + TimeSend);
                }));


            });

            socket.On("EndGame", (data) =>
            {
                str = ((Newtonsoft.Json.Linq.JObject)data)["message"].ToString();
                this.Dispatcher.Invoke((Action)(() =>
                {
                    string Time = SetTime();
                    List_Chat.Items.Add("Server: " + str + "\n" + Time);
                    socket.Disconnect();
                    Newgame = false;
                    currOption = OptionPlayer.Choose;
                }));
            });
            socket.On(Socket.EVENT_ERROR, (data) =>
            {
                this.Dispatcher.Invoke((Action)(() =>
                {
                    str = ((Newtonsoft.Json.Linq.JObject)data)["message"].ToString();
                    List_Chat.Items.Add(str);
                }));
            });

            socket.On("NextStepIs", (data) =>
            {
                this.Dispatcher.Invoke((Action)(() =>
                {
                    currOption = OptionPlayer.Online;
                    if (((Newtonsoft.Json.Linq.JObject)data)["player"].ToString() == "1")
                    {
                        ChessOnline = new node();
                        ChessOnline.row = (int)(((Newtonsoft.Json.Linq.JObject)data)["col"]);
                        ChessOnline.column = (int)(((Newtonsoft.Json.Linq.JObject)data)["row"]);
                        isCell[ChessOnline.row, ChessOnline.column] = 1;
                        DrawO(ChessOnline.row, ChessOnline.column);

                        if (RB_Computer.IsChecked == true)
                        {
                            ResetCost();
                            Evaluate();
                            node n = Getnodemax();
                            isCell[n.row, n.column] = 2;
                            socket.Emit("MyStepIs", JObject.FromObject(new { row = n.column, col = n.row }));
                            DrawX(n.row, n.column);

                        }
                        else
                            luotchoi = 1;
                    }
                }));
            });
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (currOption == OptionPlayer.Online)
                socket.Emit("MyNameIs", Text_user.Text);
        }

    }
    
}






