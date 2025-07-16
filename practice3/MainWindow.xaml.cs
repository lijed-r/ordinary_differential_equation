using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OxyPlot;
using OxyPlot.Series;
using NCalc;
using Expr = NCalc.Expression;

namespace ODESolverApp
{
    public partial class MainWindow : Window
    {
        private PlotModel plotModel;
        private ODESolver solver = new ODESolver();

        public MainWindow()
        {
            InitializeComponent();
            plotModel = new PlotModel { Title = "Решение ОДУ методом Милна" };
            plotView.Model = plotModel;
        }

        private void BtnSolve_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Парсинг входных данных
                var initialParts = txtInitial.Text.Split(';');
                double x0 = double.Parse(initialParts[0]);
                double y0 = double.Parse(initialParts[1]);

                var intervalParts = txtInterval.Text.Split(';');
                double xEnd = double.Parse(intervalParts[1]);
                double h = double.Parse(txtStep.Text);

                // Проверка корректности
                if (h <= 0) throw new ArgumentException("Шаг должен быть положительным");
                if (xEnd <= x0) throw new ArgumentException("Конец интервала должен быть больше начала");

                // Создание функции из строки
                Func<double, double, double> f = (x, y) => {
                    var expr = new Expr(txtFunction.Text);
                    expr.Parameters["x"] = x;
                    expr.Parameters["y"] = y;
                    return Convert.ToDouble(expr.Evaluate());
                };

                // Создание точного решения (если задано)
                Func<double, double> exactSolution = null;
                if (!string.IsNullOrWhiteSpace(txtExactSolution.Text))
                {
                    exactSolution = x => {
                        var expr = new Expr(txtExactSolution.Text);
                        expr.Parameters["x"] = x;
                        return Convert.ToDouble(expr.Evaluate());
                    };
                }

                // Решение ОДУ
                var solution = solver.MilneMethod(f, exactSolution, x0, y0, xEnd, h);

                DisplayResults(solution);
                PlotResults(solution);

                txtStatus.Text = "Решение успешно завершено";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Ошибка: {ex.Message}";
                MessageBox.Show($"Ошибка: {ex.Message}", "Некорректный ввод",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayResults(List<SolutionPoint> solution)
        {
            dataGrid.ItemsSource = solution;

            // Вычисление максимальной погрешности
            if (solution.Any(p => p.ExactSolution.HasValue))
            {
                double maxError = solution.Max(p => p.Error);
                txtMaxError.Text = $"Максимальная погрешность: {maxError:E4}";
            }
            else
            {
                txtMaxError.Text = "Точное решение не задано";
            }
        }

        private void PlotResults(List<SolutionPoint> solution)
        {
            plotModel.Series.Clear();
            var numericalSeries = new LineSeries
            {
                Title = "Численное решение",
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColors.Blue,
                Color = OxyColors.Blue
            };

            LineSeries exactSeries = null;
            if (solution.Any(p => p.ExactSolution.HasValue))
            {
                exactSeries = new LineSeries
                {
                    Title = "Точное решение",
                    LineStyle = LineStyle.Dash,
                    Color = OxyColors.Red
                };
            }

            foreach (var point in solution)
            {
                numericalSeries.Points.Add(new DataPoint(point.X, point.Y));
                if (exactSeries != null && point.ExactSolution.HasValue)
                {
                    exactSeries.Points.Add(new DataPoint(point.X, point.ExactSolution.Value));
                }
            }

            plotModel.Series.Add(numericalSeries);
            if (exactSeries != null) plotModel.Series.Add(exactSeries);

            plotModel.InvalidatePlot(true);
        }
    }

    public class SolutionPoint
    {
        [DisplayName("x")]
        public double X { get; set; }

        [DisplayName("y(x)")]
        public double Y { get; set; }

        [DisplayName("Точное решение")]
        public double? ExactSolution { get; set; }

        [DisplayName("Погрешность")]
        public double Error { get; set; }
    }

    public class ODESolver
    {
        public List<SolutionPoint> MilneMethod(
            Func<double, double, double> f,
            Func<double, double> exactSolution,
            double x0, double y0, double xEnd, double h)
        {
            var solution = new List<SolutionPoint>();
            int steps = (int)((xEnd - x0) / h) + 1;

            double[] x = new double[steps];
            double[] y = new double[steps];

            x[0] = x0;
            y[0] = y0;

            for (int i = 1; i < 4; i++)
            {
                x[i] = x[i - 1] + h;
                y[i] = RungeKutta4(f, x[i - 1], y[i - 1], h);
            }

            for (int i = 3; i < steps - 1; i++)
            {
                x[i + 1] = x[i] + h;

                double yPred = y[i - 3] + 4 * h / 3 *
                    (2 * f(x[i], y[i]) - f(x[i - 1], y[i - 1]) + 2 * f(x[i - 2], y[i - 2]));

                y[i + 1] = y[i - 1] + h / 3 *
                    (f(x[i - 1], y[i - 1]) + 4 * f(x[i], y[i]) + f(x[i + 1], yPred));
            }

            for (int i = 0; i < steps; i++)
            {
                double? exact = exactSolution?.Invoke(x[i]);
                double error = exact.HasValue ? Math.Abs(y[i] - exact.Value) : 0;

                solution.Add(new SolutionPoint
                {
                    X = Math.Round(x[i], 6),
                    Y = Math.Round(y[i], 6),
                    ExactSolution = exact.HasValue ? Math.Round(exact.Value, 6) : (double?)null,
                    Error = Math.Round(error, 8)
                });
            }

            return solution;
        }

        private double RungeKutta4(Func<double, double, double> f, double x, double y, double h)
        {
            double k1 = h * f(x, y);
            double k2 = h * f(x + h / 2, y + k1 / 2);
            double k3 = h * f(x + h / 2, y + k2 / 2);
            double k4 = h * f(x + h, y + k3);

            return y + (k1 + 2 * k2 + 2 * k3 + k4) / 6;
        }
    }
}