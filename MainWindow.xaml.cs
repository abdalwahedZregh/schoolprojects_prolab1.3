using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using ArticleGraphProject.Models;
using ArticleGraphProject.Services;
using ArticleGraphProject.Algorithms;
using System.Threading.Tasks;

namespace ArticleGraphProject
{
    public partial class MainWindow : Window
    {
        public enum RenderMode { Global, Expanded }
        private RenderMode _renderMode = RenderMode.Global;
        
        private Graph? _globalGraph;
        private Graph? _expandedGraph;
        
        private List<Article> _articles = new List<Article>();
        
        // Services and algorithm classes
        private JsonParser _jsonParser;
        private GraphBuilder _graphBuilder;
        private GraphExpander? _graphExpander;
        private BetweennessCalculator _betweennessCalculator;
        private KCoreDecomposer _kCoreDecomposer;


        // Node positions
        private Dictionary<string, Point> _globalNodePositions = new Dictionary<string, Point>();
        private Dictionary<string, Point> _expandedNodePositions = new Dictionary<string, Point>();
        
        // Visual element references
        private Dictionary<string, Ellipse> _nodeVisuals = new Dictionary<string, Ellipse>();
        private HashSet<string> _kCoreNodes = new HashSet<string>();
        private Dictionary<string, double> _betweennessScores = new Dictionary<string, double>();
        
        // Edge visual list
        private List<Line> _edgeVisuals = new List<Line>();
        
        // Node-edge mappings
        private Dictionary<string, List<Line>> _nodeToEdgeVisuals = new Dictionary<string, List<Line>>();
        
        // Active highlight list
        private List<Line> _activeHighlightLines = new List<Line>();
        
        // Visible node set
        private HashSet<string> _visibleNodeSet = new HashSet<string>();
        
        // Camera control
        private Point _lastMousePos;
        private bool _isPanning = false;
        
        private string? _lastSelectedArticleId = null;
        private int _visibleNodeCount = 200;
        
        // UI state
        private string _searchText = string.Empty;
        private int _filesLoadedCount = 0;
        private ToolTip? _activeTooltip;

        public MainWindow()
        {
            InitializeComponent();
            _jsonParser = new JsonParser();
            _graphBuilder = new GraphBuilder();
            _betweennessCalculator = new BetweennessCalculator();
            _kCoreDecomposer = new KCoreDecomposer();

        }

        #region Data Loading

        private void BtnLoadJson_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Makale JSON Dosyasını Seç"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Load JSON data
                    _articles = _jsonParser.LoadArticles(openFileDialog.FileName);
                    
                    // Create global graph
                    _globalGraph = _graphBuilder.BuildGlobalGraph(_articles);
                    
                    // Reset expanded graph
                    _expandedGraph = null;
                    ResetToGlobalGraph();
                    
                    _graphExpander = new GraphExpander(_globalGraph);
                    
                    // Update status
                    _filesLoadedCount++;
                    string fileName = System.IO.Path.GetFileName(openFileDialog.FileName);
                    
                    txtJsonStatus.Text = $"✔ Dosya #{_filesLoadedCount}: '{fileName}'\n" + 
                                         $"   {_articles.Count:N0} makale başarıyla yüklendi.";
                    txtJsonStatus.Foreground = Brushes.DarkGreen;
                    txtJsonStatus.FontWeight = FontWeights.Bold;

                    UpdateStatistics();
                    
                    // Enable buttons
                    btnExpand.IsEnabled = true;
                    btnBetweenness.IsEnabled = true;
                    btnKCore.IsEnabled = true;
                    btnReset.IsEnabled = true;
                    
                    // Slider settings
                    sliderVisibleNodes.Maximum = _articles.Count;
                    sliderVisibleNodes.Value = Math.Min(200, _articles.Count);
                    
                    Render();
                }
                catch (Exception ex)
                {
                    txtJsonStatus.Text = "❌ Yükleme Hatası";
                    txtJsonStatus.Foreground = Brushes.Red;
                    MessageBox.Show($"JSON yükleme hatası: {ex.Message}", "Hata", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Statistics Update

        private void UpdateStatistics()
        {
            if (_globalGraph == null) return;
            
            // Global statistics
            txtTotalNodes.Text = _globalGraph.Nodes.Count.ToString();
            txtBlackEdges.Text = _globalGraph.GetBlackEdgeCount().ToString();

            // Most cited
            var mostCited = _globalGraph.Nodes.Values
                .OrderByDescending(n => _globalGraph.GetIncomingBlackEdgeCount(n.NumericId))
                .FirstOrDefault();
            
            if (mostCited != null)
            {
                txtMostCited.Text = mostCited.NumericId;
                txtMostCitedCount.Text = _globalGraph.GetIncomingBlackEdgeCount(mostCited.NumericId).ToString();
            }

            // Most referencing
            var mostReferencing = _globalGraph.Nodes.Values
                .OrderByDescending(n => _globalGraph.GetOutgoingBlackEdgeCount(n.NumericId))
                .FirstOrDefault();
            
            if (mostReferencing != null)
            {
                txtMostReferencing.Text = mostReferencing.NumericId;
                txtMostReferencingCount.Text = _globalGraph.GetOutgoingBlackEdgeCount(mostReferencing.NumericId).ToString();
            }

            // Subgraph statistics
            if (_expandedGraph != null)
            {
                txtCurrentNodes.Text = _expandedGraph.Nodes.Count.ToString();
                txtCurrentEdges.Text = _expandedGraph.GetBlackEdgeCount().ToString();
            }
            else
            {
                txtCurrentNodes.Text = "-";
                txtCurrentEdges.Text = "-";
            }
        }

        #endregion

        #region Stage 2 H Index Expansion

        private void BtnExpand_Click(object sender, RoutedEventArgs e)
        {
            string rawInput = txtArticleId.Text;
            string articleId = NormalizeId(rawInput);

            if (string.IsNullOrWhiteSpace(articleId))
            {
                MessageBox.Show("Lütfen bir Makale ID girin", "Giriş Gerekli", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_globalGraph != null && !_globalGraph.Nodes.ContainsKey(articleId))
            {
                 MessageBox.Show($"'{rawInput}' (normalleştirilmiş: {articleId}) veri setinde bulunamadi.", "Bulunamadi", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PerformExpansion(articleId);
        }

        private string NormalizeId(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            input = input.Trim().ToUpper();
            // W prefix normalization
            return input.StartsWith("W") ? input : "W" + input;
        }

        private void PerformExpansion(string articleId)
        {
            if (_globalGraph == null || _graphExpander == null) return;

            articleId = NormalizeId(articleId);
            if (!_globalGraph.Nodes.ContainsKey(articleId))
            {
                MessageBox.Show($"'{articleId}' veri setinde bulunamadı", "Bulunamadı", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Add to existing graph
                if (_expandedGraph == null)
                {
                    _expandedGraph = new Graph();
                }
                
                // H-Index calculation
                var hIndexResult = _graphExpander.ExpandGraph(_expandedGraph, articleId);
                
                if (hIndexResult.HIndex == 0)
                {
                    MessageBox.Show(
                        "Bu makalenin H-indeksi 0 olduğu için genişletilmiyor.",
                        "Genişletme Mümkün Değil",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                _lastSelectedArticleId = articleId;
                
                // Clear old highlights
                foreach (var node in _expandedGraph.Nodes.Values) node.State = NodeState.Normal;
                
                // Highlight selected node
                if (_expandedGraph.Nodes.ContainsKey(articleId))
                    _expandedGraph.Nodes[articleId].State = NodeState.Selected;
                
                // Highlight H-Core nodes
                foreach (var coreId in hIndexResult.HCore)
                {
                    if (_expandedGraph.Nodes.ContainsKey(coreId))
                        _expandedGraph.Nodes[coreId].State = NodeState.HCore;
                }
                
                // Update statistics
                txtSelectedArticle.Text = articleId;
                txtHIndex.Text = hIndexResult.HIndex.ToString();
                txtHCoreSize.Text = hIndexResult.HCore.Count.ToString();
                txtHMedian.Text = hIndexResult.HMedian.ToString("F2");
                
                UpdateStatistics();
                _renderMode = RenderMode.Expanded;
                
                // Reset camera
                canvasTranslateTransform.X = 0;
                canvasTranslateTransform.Y = 0;
                canvasScaleTransform.ScaleX = 1;
                canvasScaleTransform.ScaleY = 1;

                Render();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Genişletme hatası: {ex.Message}", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Stage 3 Metrics

        private async void BtnBetweenness_Click(object sender, RoutedEventArgs e)
        {
            if (_renderMode != RenderMode.Expanded || _expandedGraph == null || _expandedGraph.Nodes.Count == 0)
            {
                MessageBox.Show("Lütfen önce bir makale seçip grafiği genişletin.", 
                    "Grafik Boş", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Temporarily disable UI
                btnBetweenness.IsEnabled = false;
                btnExpand.IsEnabled = false;
                btnReset.IsEnabled = false;
                btnKCore.IsEnabled = false;
                txtMetricResults.Text = "Hesaplanıyor... (büyük ağlarda biraz sürebilir)";

                var betweennessDict = await Task.Run(() => 
                {
                    return _betweennessCalculator.CalculateBetweenness(_expandedGraph);
                });
                
                // Store results
                _betweennessScores = betweennessDict;
                
                var betweenness = _betweennessCalculator.GetSortedBetweenness(betweennessDict);

                var results = "Betweenness Centrality İlk 20:\n\n";
                foreach (var item in betweenness.Take(20))
                {
                    results += $"{item.NodeId}: {item.Betweenness:F2}\n";
                }
                
                txtMetricResults.Text = results;
                
                // Redraw for visualization
                Render();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hesaplama hatası: {ex.Message}", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtMetricResults.Text = "Hata oluştu.";
            }
            finally
            {
                // Re-enable UI
                btnBetweenness.IsEnabled = true;
                btnExpand.IsEnabled = true;
                btnReset.IsEnabled = true;
                btnKCore.IsEnabled = true;
                UpdateStatistics();
                if (_renderMode == RenderMode.Expanded) Render();
            }
        }

        private void BtnKCore_Click(object sender, RoutedEventArgs e)
        {
            if (_renderMode != RenderMode.Expanded || _expandedGraph == null || _expandedGraph.Nodes.Count == 0)
            {
                MessageBox.Show("Lütfen önce bir makale seçip grafiği genişletin.", 
                    "Grafik Boş", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtKValue.Text, out int k) || k < 1)
            {
                MessageBox.Show("Lütfen geçerli bir K değeri girin (pozitif tam sayı).", "Geçersiz Giriş", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Convert to undirected graph
                var undirected = _expandedGraph.ToUndirected();
                _kCoreNodes = _kCoreDecomposer.FindKCore(undirected, k);

                // Update node states
                foreach (var node in _expandedGraph.Nodes.Values)
                {
                    if (_kCoreNodes.Contains(node.NumericId))
                    {
                        node.State = NodeState.KCore;
                    }
                    else if (node.NumericId != _lastSelectedArticleId)
                    {
                        node.State = NodeState.Normal;
                    }
                }
                
                txtMetricResults.Text = $"K-Core (K={k}):\n\n" +
                    $"Düğüm sayısı: {_kCoreNodes.Count}\n\n" +
                    string.Join(", ", _kCoreNodes.Take(50));
                
                if (_renderMode == RenderMode.Expanded) Render();
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"K-Core hatası: {ex.Message}", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Reset

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetToGlobalGraph();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = txtSearch.Text.Trim();
            if (_nodeVisuals == null) return;

            // Apply search filter
            foreach (var kvp in _nodeVisuals)
            {
                var nodeId = kvp.Key;
                var ellipse = kvp.Value;
                bool isMatch = string.IsNullOrEmpty(_searchText) ||
                               nodeId.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
                ellipse.Opacity = isMatch ? 1.0 : 0.1;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ResetToGlobalGraph();
            }
            else if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                txtSearch.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.R && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ResetToGlobalGraph();
                e.Handled = true;
            }
        }

        private void ResetToGlobalGraph()
        {
            if (_globalGraph == null) return;
            
            _globalGraph.ClearCache();
            _expandedGraph = null;
            _expandedNodePositions.Clear();
            _renderMode = RenderMode.Global;
            _kCoreNodes.Clear();
            _betweennessScores.Clear();
            
            _lastSelectedArticleId = null;
            // Reset UI labels
            txtSelectedArticle.Text = "-";
            txtHIndex.Text = "-";
            txtHCoreSize.Text = "-";
            txtHMedian.Text = "-";
            txtMetricResults.Text = "Henüz metrik hesaplanmadı";
            
            UpdateStatistics();
            
            // Reset zoom and pan
            canvasScaleTransform.ScaleX = 1;
            canvasScaleTransform.ScaleY = 1;
            canvasTranslateTransform.X = 0;
            canvasTranslateTransform.Y = 0;
            
            Render();
        }

        #endregion

        #region Rendering Logic

        private void Render()
        {
            graphCanvas.Children.Clear();
            _nodeVisuals.Clear();
            _edgeVisuals.Clear();
            _nodeToEdgeVisuals.Clear();
            _activeHighlightLines.Clear();

            // Return to global mode if expanded graph is empty
            if (_renderMode == RenderMode.Expanded && (_expandedGraph == null || _expandedGraph.Nodes.Count == 0))
            {
                _renderMode = RenderMode.Global;
            }

            switch (_renderMode)
            {
                case RenderMode.Expanded:
                    RenderExpandedGraph();
                    break;
                case RenderMode.Global:
                default:
                    if (_globalGraph != null) RenderGlobalGraph();
                    break;
            }
        }

        private void RenderGlobalGraph()
        {
            if (_globalGraph == null) return;

            if (_globalNodePositions.Count == 0) CalculateGlobalLayout();

            _visibleNodeSet.Clear();
            // Select nodes by importance
            var importantNodes = _globalGraph.Nodes.Values
                .Select(n => new
                {
                    Node = n,
                    Importance = _globalGraph.GetIncomingBlackEdgeCount(n.NumericId) + 
                                 _globalGraph.GetOutgoingBlackEdgeCount(n.NumericId)
                })
                .OrderByDescending(x => x.Importance)
                .Take(_visibleNodeCount)
                .Select(x => x.Node.NumericId);
                
            foreach (var id in importantNodes) _visibleNodeSet.Add(id);

            DrawGlobalEdges();
            DrawGlobalNodes();
        }

        private void CalculateGlobalLayout()
        {
            if (_globalGraph == null) return;
            _globalNodePositions.Clear();

            double centerX = graphCanvas.ActualWidth / 2;
            double centerY = graphCanvas.ActualHeight / 2;
            
            // Spiral layout calculation
            var sortedNodes = _globalGraph.Nodes.Values
                .OrderByDescending(n => _globalGraph.GetIncomingBlackEdgeCount(n.NumericId) + 
                                      _globalGraph.GetOutgoingBlackEdgeCount(n.NumericId))
                .ToList();

            double angle = 0;
            double radius = 10;
            double radiusStep = 0.5;
            double angleStep = 0.5;
            double minNodeDist = 20;

            if (sortedNodes.Count > 0)
                _globalNodePositions[sortedNodes[0].NumericId] = new Point(centerX, centerY);

            for (int i = 1; i < sortedNodes.Count; i++)
            {
                int attempts = 0;
                bool placed = false;
                while (!placed && attempts < 100)
                {
                    double x = centerX + radius * Math.Cos(angle);
                    double y = centerY + radius * Math.Sin(angle);
                    _globalNodePositions[sortedNodes[i].NumericId] = new Point(x, y);
                    placed = true;
                    
                    angle += angleStep;
                    if (radius > 50) angleStep = Math.Max(0.1, 0.5 * (50 / radius));
                    radius += (minNodeDist / (2 * Math.PI * radius)) * 5;
                    if (radiusStep < 0.1) radiusStep = 0.1;
                    radius += radiusStep;
                }
            }
        }

        private void DrawGlobalEdges()
        {
            if (_globalGraph == null) return;
            
            foreach (var edge in _globalGraph.Edges)
            {
                // Skip edges of invisible nodes
                if (_visibleNodeSet.Count > 0)
                {
                    if (!_visibleNodeSet.Contains(edge.From) || !_visibleNodeSet.Contains(edge.To)) continue;
                }

                if (!_globalNodePositions.ContainsKey(edge.From) || !_globalNodePositions.ContainsKey(edge.To)) continue;

                var fromPos = _globalNodePositions[edge.From];
                var toPos = _globalNodePositions[edge.To];

                var line = new Line
                {
                    X1 = fromPos.X, Y1 = fromPos.Y,
                    X2 = toPos.X, Y2 = toPos.Y
                };

                if (edge.EdgeType == EdgeType.Black)
                {
                    line.Stroke = Brushes.Black;
                    line.StrokeThickness = 1.2;
                    line.Opacity = 0.6;
                }
                else
                {
                    // Green edge style
                    line.Stroke = Brushes.LightGreen;
                    line.StrokeThickness = 0.8;
                    line.Opacity = 0.08;
                    line.StrokeDashArray = new DoubleCollection { 2, 2 };
                }

                line.Tag = edge;
                graphCanvas.Children.Add(line);
                _edgeVisuals.Add(line);
                
                // Register edge visuals
                RegisterEdgeVisual(edge.From, line);
                RegisterEdgeVisual(edge.To, line);
            }
        }

        private void DrawGlobalNodes()
        {
            if (_globalGraph == null) return;
            
            foreach (var node in _globalGraph.Nodes.Values)
            {
                if (_visibleNodeSet.Count > 0 && !_visibleNodeSet.Contains(node.NumericId)) continue;
                if (!_globalNodePositions.ContainsKey(node.NumericId)) continue;

                var pos = _globalNodePositions[node.NumericId];
                var ellipse = new Ellipse
                {
                    Width = 3, Height = 3,
                    Fill = new SolidColorBrush(Color.FromRgb(180, 180, 200))
                };
                
                Canvas.SetLeft(ellipse, pos.X - 1.5);
                Canvas.SetTop(ellipse, pos.Y - 1.5);

                // Set opacity according to search filter
                if (!string.IsNullOrEmpty(_searchText) && 
                    !node.NumericId.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                {
                    ellipse.Opacity = 0.3;
                }
                else
                {
                    ellipse.Opacity = 1.0;
                }

                _nodeVisuals[node.NumericId] = ellipse;
                graphCanvas.Children.Add(ellipse);

                // Click events
                ellipse.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    if (Keyboard.Modifiers == ModifierKeys.Control) HandlePathSelection(node.NumericId);
                    else PerformExpansion(node.NumericId);
                };
                
                // Mouse interaction events
                ellipse.MouseEnter += (s, e) => { ShowNodeTooltip(node, ellipse); HighlightConnectedEdges(node.NumericId); };
                ellipse.MouseLeave += (s, e) => { HideNodeTooltip(); ResetEdgeHighlight(); };
            }
        }

        private void RenderExpandedGraph()
        {
            if (_expandedGraph == null) return;
            
            _visibleNodeSet.Clear();
            foreach (var nodeId in _expandedGraph.Nodes.Keys) _visibleNodeSet.Add(nodeId);

            CalculateExpandedLayout();
            DrawExpandedEdges();
            DrawExpandedNodes();
        }

        private void CalculateExpandedLayout()
        {
            if (_expandedGraph == null) return;
            
            double centerX = graphCanvas.ActualWidth / 2;
            double centerY = graphCanvas.ActualHeight / 2;
            Point centerPoint = new Point(centerX, centerY);

            if (_expandedNodePositions.Count == 0 && _lastSelectedArticleId != null)
                _expandedNodePositions[_lastSelectedArticleId] = centerPoint;

            var newNodes = _expandedGraph.Nodes.Keys
                .Where(id => !_expandedNodePositions.ContainsKey(id))
                .ToList();
            
            if (newNodes.Count == 0) return;

            // Place new nodes around the center
            Point sourcePos = centerPoint;
            if (_lastSelectedArticleId != null && _expandedNodePositions.ContainsKey(_lastSelectedArticleId))
                sourcePos = _expandedNodePositions[_lastSelectedArticleId];

            double radius = 150;
            // Adjust radius based on node count
            if (newNodes.Count > 20) radius += (newNodes.Count - 20) * 2;

            for (int i = 0; i < newNodes.Count; i++)
            {
                double angle = 2 * Math.PI * i / newNodes.Count;
                // Circular layout
                angle += _expandedNodePositions.Count * 0.1;
                double x = sourcePos.X + radius * Math.Cos(angle);
                double y = sourcePos.Y + radius * Math.Sin(angle);
                _expandedNodePositions[newNodes[i]] = new Point(x, y);
            }
        }

        private void DrawExpandedEdges()
        {
            if (_expandedGraph == null) return;
            
            foreach (var edge in _expandedGraph.Edges)
            {
                if (!_expandedNodePositions.ContainsKey(edge.From) || !_expandedNodePositions.ContainsKey(edge.To)) continue;

                var fromPos = _expandedNodePositions[edge.From];
                var toPos = _expandedNodePositions[edge.To];

                var line = new Line
                {
                    X1 = fromPos.X, Y1 = fromPos.Y,
                    X2 = toPos.X, Y2 = toPos.Y
                };

                if (edge.EdgeType == EdgeType.Black)
                {
                    // K-Core edge check
                    bool isKCoreEdge = _kCoreNodes.Contains(edge.From) && _kCoreNodes.Contains(edge.To);
                    
                    if (isKCoreEdge)
                    {
                        line.Stroke = Brushes.DarkMagenta;
                        line.StrokeThickness = 2.5;
                        line.Opacity = 0.9;
                    }
                    else
                    {
                        line.Stroke = Brushes.Black;
                        line.StrokeThickness = 1.5;
                        line.Opacity = 0.7;
                    }
                }
                else
                {
                    // Green edge style
                    line.Stroke = Brushes.LightGreen;
                    line.StrokeThickness = 0.8;
                    line.Opacity = 0.08;
                    line.StrokeDashArray = new DoubleCollection { 2, 2 };
                }

                line.Tag = edge;
                graphCanvas.Children.Add(line);
                _edgeVisuals.Add(line);
                
                // Register edge visuals
                RegisterEdgeVisual(edge.From, line);
                RegisterEdgeVisual(edge.To, line);
            }
        }

        private void RegisterEdgeVisual(string nodeId, Line line)
        {
            if (!_nodeToEdgeVisuals.ContainsKey(nodeId))
            {
                _nodeToEdgeVisuals[nodeId] = new List<Line>();
            }
            _nodeToEdgeVisuals[nodeId].Add(line);
        }

        private void DrawExpandedNodes()
        {
            if (_expandedGraph == null) return;
            
            foreach (var node in _expandedGraph.Nodes.Values)
            {
                if (!_expandedNodePositions.ContainsKey(node.NumericId)) continue;
                var pos = _expandedNodePositions[node.NumericId];

                // Sizing by citation count
                int citations = _globalGraph != null ? _globalGraph.GetIncomingBlackEdgeCount(node.NumericId) : 0;
                double nodeSize = Math.Max(20, 30 + Math.Log(citations + 1) * 4.0);
                
                // Coloring by node state
                Brush nodeFill = Brushes.LightGray;
                Brush nodeStroke = Brushes.DimGray;
                double strokeThickness = 1.0;
                
                if (node.State == NodeState.KCore)
                {
                    // K-Core nodes: Purple with thick border
                    nodeFill = Brushes.MediumPurple;
                    nodeStroke = Brushes.DarkMagenta;
                    strokeThickness = 3.0;
                }
                else if (node.State == NodeState.Selected)
                {
                    // Selected node: Salmon/Red
                    nodeFill = Brushes.Salmon;
                    nodeStroke = Brushes.DarkRed;
                    strokeThickness = 2.5;
                }
                else if (node.State == NodeState.HCore)
                {
                    // H-Core nodes: Gold
                    nodeFill = Brushes.Gold;
                    nodeStroke = Brushes.DarkGoldenrod;
                    strokeThickness = 2.0;
                }
                else if (citations > 10)
                {
                    // High citation normal nodes: Light blue
                    nodeFill = Brushes.LightSkyBlue;
                }

                var ellipse = new Ellipse
                {
                    Width = nodeSize, 
                    Height = nodeSize,
                    Fill = nodeFill, 
                    Stroke = nodeStroke, 
                    StrokeThickness = strokeThickness
                };

                // Search filter opacity
                if (!string.IsNullOrEmpty(_searchText) && 
                    !node.NumericId.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                {
                    ellipse.Opacity = 0.3;
                }
                else
                {
                    ellipse.Opacity = 1.0;
                }

                Canvas.SetLeft(ellipse, pos.X - nodeSize / 2);
                Canvas.SetTop(ellipse, pos.Y - nodeSize / 2);

                _nodeVisuals[node.NumericId] = ellipse;
                graphCanvas.Children.Add(ellipse);

                // Labels
                var countLabel = new TextBlock
                {
                    Text = citations.ToString(),
                    FontSize = Math.Max(10, nodeSize / 2.5),
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Black,
                    IsHitTestVisible = false
                };
                
                countLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(countLabel, pos.X - countLabel.DesiredSize.Width / 2);
                Canvas.SetTop(countLabel, pos.Y - countLabel.DesiredSize.Height / 2);
                
                if (ellipse.Opacity > 0.5) graphCanvas.Children.Add(countLabel);

                var detailsLabel = new TextBlock
                {
                    Text = $"{node.NumericId}\n{node.AuthorInitials}",
                    FontSize = 10, Foreground = Brushes.Black,
                    TextAlignment = TextAlignment.Center, IsHitTestVisible = false
                };
                
                detailsLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(detailsLabel, pos.X - detailsLabel.DesiredSize.Width / 2);
                Canvas.SetTop(detailsLabel, pos.Y + nodeSize / 2 + 2);

                if (ellipse.Opacity > 0.5) graphCanvas.Children.Add(detailsLabel);

                // Betweenness score display
                if (_betweennessScores.ContainsKey(node.NumericId))
                {
                    var betweennessLabel = new TextBlock
                    {
                        Text = $"BC: {_betweennessScores[node.NumericId]:F1}",
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.DarkBlue,
                        Background = new SolidColorBrush(Color.FromArgb(180, 255, 255, 200)), // Semi-transparent yellow
                        Padding = new Thickness(2),
                        IsHitTestVisible = false
                    };
                    
                    betweennessLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(betweennessLabel, pos.X - betweennessLabel.DesiredSize.Width / 2);
                    Canvas.SetTop(betweennessLabel, pos.Y + nodeSize / 2 + 18);
                    
                    if (ellipse.Opacity > 0.5) graphCanvas.Children.Add(betweennessLabel);
                }

                // Event handlers
                ellipse.MouseEnter += (s, e) => { ShowNodeTooltip(node, ellipse); HighlightConnectedEdges(node.NumericId); };
                ellipse.MouseLeave += (s, e) => { HideNodeTooltip(); ResetEdgeHighlight(); };
                ellipse.MouseLeftButtonDown += (s, e) => { e.Handled = true; PerformExpansion(node.NumericId); };
            }
        }

        #endregion

        #region Interaction Helpers

        private void HighlightConnectedEdges(string nodeId)
        {
            // O(1) dictionary lookup
            if (_nodeToEdgeVisuals.TryGetValue(nodeId, out var connectedLines))
            {
                foreach (var line in connectedLines)
                {
                    if (line.Tag is GraphEdge edge)
                    {
                        // Apply highlight
                        line.Opacity = 1.0;
                        if (edge.From == nodeId)
                        {
                            line.Stroke = Brushes.Orange;
                            Panel.SetZIndex(line, 50);
                        }
                        else if (edge.To == nodeId)
                        {
                            line.Stroke = Brushes.DeepSkyBlue;
                            Panel.SetZIndex(line, 50);
                        }
                        
                        _activeHighlightLines.Add(line);
                    }
                }
            }
        }

        private void ResetEdgeHighlight()
        {
            // Reset only highlighted edges
            foreach (var line in _activeHighlightLines)
            {
                if (line.Tag is GraphEdge edge)
                {
                    if (edge.EdgeType == EdgeType.Black)
                    {
                        line.Stroke = Brushes.Black;
                        line.Opacity = (_renderMode == RenderMode.Global) ? 0.6 : 0.75;
                    }
                    else
                    {
                        // Green edge default style
                        line.Stroke = Brushes.LightGreen;
                        line.Opacity = 0.08;
                    }
                    Panel.SetZIndex(line, 0);
                }
            }
            _activeHighlightLines.Clear();
        }

        private void ShowNodeTooltip(GraphNode node, Ellipse ellipse)
        {
            // Close previous tooltip
            HideNodeTooltip();

            string tooltipText = $"ID: {node.NumericId}\n" +
                $"Başlık: {node.ArticleData.Title}\n" +
                $"Yazarlar: {(node.ArticleData.Authors.Any() ? string.Join(", ", node.ArticleData.Authors) : "Yok")}\n" +
                $"Yıl: {node.ArticleData.Year}\n" +
                $"Atıf (Gelen): {_globalGraph?.GetIncomingBlackEdgeCount(node.NumericId) ?? 0}";
                
            _activeTooltip = new ToolTip
            {
                Content = new TextBlock { Text = tooltipText, MaxWidth = 300, TextWrapping = TextWrapping.Wrap },
                PlacementTarget = ellipse,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                IsOpen = true
            };
        }
        
        private void HideNodeTooltip() 
        { 
            if (_activeTooltip != null)
            {
                _activeTooltip.IsOpen = false;
                _activeTooltip = null;
            }
        }

        private void SliderVisibleNodes_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _visibleNodeCount = (int)e.NewValue;
            if (txtVisibleNodesLabel != null) txtVisibleNodesLabel.Text = $"Görünen Düğüm: {_visibleNodeCount}";
            // Set slider maximum value
            if (_globalGraph != null && sliderVisibleNodes != null) sliderVisibleNodes.Maximum = _globalGraph.Nodes.Count;
            if (_renderMode == RenderMode.Global && _globalGraph != null) Render();
        }

        private void GraphCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
            Point mousePos = e.GetPosition(graphCanvas);
            double newScaleX = canvasScaleTransform.ScaleX * zoomFactor;
            double newScaleY = canvasScaleTransform.ScaleY * zoomFactor;

            // Zoom limits
            if (newScaleX < 0.1 || newScaleX > 5.0) return;

            // Zoom towards mouse position
            canvasTranslateTransform.X = mousePos.X - (mousePos.X - canvasTranslateTransform.X) * zoomFactor;
            canvasTranslateTransform.Y = mousePos.Y - (mousePos.Y - canvasTranslateTransform.Y) * zoomFactor;
            canvasScaleTransform.ScaleX = newScaleX;
            canvasScaleTransform.ScaleY = newScaleY;
            e.Handled = true;
        }

        private void GraphCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Start panning
            if (e.OriginalSource == graphCanvas && e.LeftButton == MouseButtonState.Pressed && e.RightButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                _lastMousePos = e.GetPosition(this);
                graphCanvas.CaptureMouse();
                graphCanvas.Cursor = Cursors.Hand;
                e.Handled = true;
            }
        }

        private void GraphCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point currentPos = e.GetPosition(this);
                Vector delta = currentPos - _lastMousePos;
                canvasTranslateTransform.X += delta.X;
                canvasTranslateTransform.Y += delta.Y;
                _lastMousePos = currentPos;
            }
        }

        private void GraphCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                graphCanvas.ReleaseMouseCapture();
                graphCanvas.Cursor = Cursors.Arrow;
            }
        }
        
        private void GraphCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e) { e.Handled = true; }

        private void HandlePathSelection(string nodeId) { }
        
        #endregion
    }
}
