#!/usr/bin/env python3
"""
Academic Style GPU Profiler Analyzer - Bar Chart Version
学术风格GPU性能分析器，专注于清晰、专业的柱状图展示
"""

import json
import matplotlib.pyplot as plt
import pandas as pd
import numpy as np
from pathlib import Path
import argparse
import sys
from datetime import datetime
import warnings

warnings.filterwarnings('ignore')

class AcademicGPUBarAnalyzer:
    def __init__(self, json_path, custom_config=None, kernel_name_mapping=None):
        """Initialize analyzer with academic styling focus."""
        self.json_path = Path(json_path)
        self.data = self.load_data()
        self.kernel_name_mapping = kernel_name_mapping or self.get_default_kernel_mapping()
        self.custom_config = custom_config or {}
        self.df = self.create_dataframe()
        self.output_dir = self.json_path.parent
        
        # 设置学术风格的matplotlib参数
        plt.rcParams['font.family'] = 'serif'
        plt.rcParams['font.serif'] = ['Times New Roman', 'DejaVu Serif', 'serif']
        plt.rcParams['mathtext.fontset'] = 'stix'
        plt.rcParams['font.size'] = 10
        
    def get_default_kernel_mapping(self):
        """Default kernel name mapping for academic papers."""
        return {
            'SolveXPBDHydrostatic': 'Hydrostatic Solver',
            'SolveXPBDDeviatoric': 'Deviatoric Solver',
            'UpdateSurfaceMesh': 'Surface Update',
            'UpdateStressVisualization': 'Stress Visualization',
            'PredictPositions': 'Position Prediction',
            'CollideSDF': 'SDF Collision',
            'CollideSDF_PreStep': 'SDF Pre-Step',
            'CollideParticles': 'Particle Collision',
            'SolveAttachment': 'Attachment Solver',
            'ApplyDeltas': 'Delta Application',
            'XPBDFinalize': 'XPBD Finalization',
            'UpdateMotionConstraints': 'Motion Constraints',
            'SolveConstraints': 'Constraint Solving',
            'ComputeAverageResidual': 'Residual Computation',
            'SolveStableNeoHookean': 'Neo-Hookean Solver',
            'UpdateSpatialHash': 'Spatial Hashing',
            'NormalizeSurfaceNormals': 'Normal Computation',
            'UpdateSurfaceNormals': 'Surface Normals',
            'UpdateSurfaceVertices': 'Surface Vertices'
        }
    
    def load_data(self):
        """Load profiling data from JSON file."""
        try:
            with open(self.json_path, 'r', encoding='utf-8') as f:
                return json.load(f)
        except FileNotFoundError:
            raise FileNotFoundError(f"❌ Profiling results file not found: {self.json_path}")
        except json.JSONDecodeError as e:
            raise ValueError(f"❌ Invalid JSON format: {e}")
    
    def create_dataframe(self):
        """Convert kernel statistics to pandas DataFrame."""
        if 'kernel_statistics' not in self.data:
            raise ValueError("❌ Invalid JSON format: missing 'kernel_statistics'")
            
        kernel_stats = self.data['kernel_statistics']
        if not kernel_stats:
            raise ValueError("❌ No kernel statistics found in the data")
            
        df = pd.DataFrame(kernel_stats)
        
        # Apply kernel name mapping
        df['display_name'] = df['name'].apply(
            lambda x: self.kernel_name_mapping.get(x, x)
        )
        
        # Sort by percentage descending
        df = df.sort_values('percentage', ascending=False).reset_index(drop=True)
        return df
    
    def get_academic_colors(self, n_colors):
        """
        使用 Tableau 10 调色板：
        经典且对比度友好，最多支持 10 种颜色。
        """
        palette = [
            '#4E79A7', '#F28E2B', '#E15759', 
            '#76B7B2', '#59A14F', '#EDC948', 
            '#B07AA1', '#FF9DA7', '#9C755F', '#BAB0AC'
        ]
        if n_colors <= len(palette):
            return palette[:n_colors]
        # 超过 10 时，退回到 tab20
        cmap = plt.get_cmap('tab20')
        return [cmap(i / n_colors) for i in range(n_colors)]

    def prepare_display_data(self):
        """Prepare display data with detailed debugging and normalization."""
        display_items = self.custom_config.get("manual_display", [])
        
        if not display_items:
            print("📋 No manual display configuration found, using top kernels...")
            # 如果没有配置，使用默认显示前8个内核
            top_kernels = self.df.head(8)
            labels = top_kernels['display_name'].tolist()
            sizes = top_kernels['percentage'].tolist()
            
            # 添加其他项
            if len(self.df) > 8:
                others_total = self.df.iloc[8:]['percentage'].sum()
                labels.append(f'Others ({len(self.df)-8})')
                sizes.append(others_total)
            
            colors = self.get_academic_colors(len(labels))
            return labels, sizes, colors
        
        labels = []
        sizes = []
        colors = []
        used_kernels = set()
        
        print(f"📋 Processing {len(display_items)} display items...")
        print(f"📊 Available kernels in data: {list(self.df['name'].values)}")
        
        for i, item in enumerate(display_items):
            item_type = item.get("type")
            print(f"\n   {i+1}. Processing {item_type} item...")
            
            if item_type == "individual":
                kernel_name = item.get("kernel")
                print(f"      Looking for kernel: '{kernel_name}'")
                
                if kernel_name in self.df['name'].values:
                    kernel_data = self.df[self.df['name'] == kernel_name].iloc[0]
                    
                    display_name = item.get("display_name")
                    if display_name is None:
                        display_name = kernel_data['display_name']
                    
                    labels.append(display_name)
                    sizes.append(kernel_data['percentage'])
                    colors.append(item.get("color"))
                    used_kernels.add(kernel_name)
                    
                    print(f"      ✅ Added individual: {display_name} ({kernel_data['percentage']:.1f}%)")
                else:
                    print(f"      ❌ Kernel '{kernel_name}' not found in data")
                    print(f"         Available kernels: {list(self.df['name'].values)}")
            
            elif item_type == "merged":
                kernel_names = item.get("kernels", [])
                print(f"      Looking for kernels: {kernel_names}")
                
                total_percentage = 0
                found_kernels = []
                
                for kernel_name in kernel_names:
                    if kernel_name in self.df['name'].values:
                        kernel_data = self.df[self.df['name'] == kernel_name].iloc[0]
                        total_percentage += kernel_data['percentage']
                        found_kernels.append(kernel_name)
                        used_kernels.add(kernel_name)
                        print(f"         ✓ Found: {kernel_name} ({kernel_data['percentage']:.1f}%)")
                    else:
                        print(f"         ✗ Missing: {kernel_name}")
                
                if found_kernels:
                    display_name = item.get("display_name", f"Merged ({len(found_kernels)})")
                    labels.append(display_name)
                    sizes.append(total_percentage)
                    colors.append(item.get("color"))
                    
                    print(f"      ✅ Added merged: {display_name} ({total_percentage:.1f}%, {len(found_kernels)} kernels)")
                else:
                    print(f"      ❌ No kernels found for merged item: {kernel_names}")
            
            elif item_type == "others":
                if item.get("include_remaining", False):
                    remaining_kernels = self.df[~self.df['name'].isin(used_kernels)]
                    if len(remaining_kernels) > 0:
                        others_total = remaining_kernels['percentage'].sum()
                        display_name = item.get("display_name", f"Others ({len(remaining_kernels)})")
                        
                        labels.append(display_name)
                        sizes.append(others_total)
                        colors.append(item.get("color"))
                        
                        print(f"      ✅ Added others: {display_name} ({others_total:.1f}%, {len(remaining_kernels)} kernels)")
        
        # 检查是否有遗漏的重要内核
        unused_kernels = self.df[~self.df['name'].isin(used_kernels)]
        if len(unused_kernels) > 0:
            print(f"\n⚠️  Unused kernels found:")
            for _, kernel in unused_kernels.iterrows():
                print(f"      {kernel['name']}: {kernel['percentage']:.1f}%")
        
        # 填充缺失的颜色
        base_colors = self.get_academic_colors(len(labels))
        for i in range(len(colors)):
            if colors[i] is None:
                colors[i] = base_colors[i]
        
        # 归一化百分比确保总和为100%
        total = sum(sizes)
        if abs(total - 100.0) > 0.01:  # 如果总和不是100%，进行归一化
            # 按比例调整所有值
            normalized_sizes = [size * 100.0 / total for size in sizes]
            # 四舍五入到一位小数
            rounded_sizes = [round(size, 1) for size in normalized_sizes]
            
            # 检查四舍五入后的总和
            rounded_total = sum(rounded_sizes)
            if abs(rounded_total - 100.0) > 0.05:
                # 调整最大的项来确保总和为100.0
                difference = 100.0 - rounded_total
                max_index = sizes.index(max(sizes))
                rounded_sizes[max_index] = round(rounded_sizes[max_index] + difference, 1)
                print(f"📊 Normalized percentages (total was {total:.2f}%, adjusted by {difference:.1f}%)")
            
            sizes = rounded_sizes
        
        print(f"\n📊 Final data summary:")
        print(f"   Components: {len(labels)}")
        print(f"   Total percentage: {sum(sizes):.1f}%")
        for label, size in zip(labels, sizes):
            print(f"   - {label}: {size:.1f}%")
        
        return labels, sizes, colors
    
    def create_academic_bar_chart(self, save_path=None, show_plot=True):
        """Create clean, academic-style horizontal bar chart."""
        
        # Prepare data
        labels, sizes, custom_colors = self.prepare_display_data()
        
        if not labels:
            print("❌ No data to display. Check your configuration.")
            return
        
        # Get styling configuration
        style = self.custom_config.get("chart_style", {})
        figsize = style.get("figsize", [12, 8])
        
        # Create figure with academic styling
        fig, ax = plt.subplots(figsize=figsize)
        fig.patch.set_facecolor('white')
        
        # 使用学术颜色方案
        if custom_colors and all(c is not None for c in custom_colors):
            final_colors = custom_colors
        else:
            base_colors = self.get_academic_colors(len(labels))
            final_colors = []
            for i in range(len(labels)):
                if i < len(custom_colors) and custom_colors[i] is not None:
                    final_colors.append(custom_colors[i])
                else:
                    final_colors.append(base_colors[i])
        
        print(f"🎨 Using colors: {final_colors}")
        
        # 数据按百分比排序（从上到下递减）
        combined_data = list(zip(labels, sizes, final_colors))
        combined_data.sort(key=lambda x: x[1], reverse=True)
        labels, sizes, final_colors = zip(*combined_data)
        
        # 创建y轴位置
        y_pos = np.arange(len(labels))
        
        # 绘制水平柱状图
        bars = ax.barh(
            y_pos, 
            sizes, 
            color=final_colors,
            alpha=style.get("alpha", 0.85),
            edgecolor=style.get("edge_color", "white"),
            linewidth=style.get("edge_width", 1.0),
            height=style.get("bar_height", 0.7)
        )
        
        # 设置y轴标签 - 增强显示效果
        ax.set_yticks(y_pos)
        
        # 获取标签显示配置 - ylabel_config优先级最高
        label_config = style.get("ylabel_config", {})
        if label_config:
            # 如果有ylabel_config，完全使用其设置
            label_fontsize = label_config.get("fontsize", 11)
            label_fontweight = label_config.get("fontweight", "bold")
            label_color = label_config.get("color", "black")
            use_bbox = label_config.get("use_bbox", True)
            bbox_alpha = label_config.get("bbox_alpha", 0.8)
            bbox_color = label_config.get("bbox_color", "white")
            bbox_edge_color = label_config.get("bbox_edge_color", "lightgray")
        else:
            # 回退到通用label_fontsize设置
            label_fontsize = style.get("label_fontsize", 11)
            label_fontweight = "normal"
            label_color = "black"
            use_bbox = False
            bbox_alpha = 0.8
            bbox_color = "white"
            bbox_edge_color = "lightgray"
        
        # 设置y轴标签with增强效果 - 强制应用字体大小
        print(f"🔍 Debug: Applying fontsize {label_fontsize} to y-axis labels")
        
        if use_bbox:
            # 使用背景框突出显示
            bbox_props = dict(
                boxstyle="round,pad=0.3", 
                facecolor=bbox_color, 
                edgecolor=bbox_edge_color, 
                alpha=bbox_alpha,
                linewidth=0.5
            )
            # 直接设置每个标签以确保字体大小生效
            ax.set_yticklabels(
                labels, 
                fontsize=label_fontsize,
                fontweight=label_fontweight,
                color=label_color,
                bbox=bbox_props
            )
        else:
            # 仅使用字体增强
            ax.set_yticklabels(
                labels, 
                fontsize=label_fontsize,
                fontweight=label_fontweight,
                color=label_color
            )
        
        # 额外确保字体大小：直接操作每个标签对象
        for tick_label in ax.get_yticklabels():
            tick_label.set_fontsize(label_fontsize)
            tick_label.set_fontweight(label_fontweight)
            tick_label.set_color(label_color)
        
        # 设置x轴 - 增强配置选项
        xlabel_config = style.get("xlabel_config", {})
        xlabel_text = xlabel_config.get("text", "Percentage (%)")
        xlabel_fontsize = xlabel_config.get("fontsize", style.get("axis_label_fontsize", 12))
        xlabel_fontweight = xlabel_config.get("fontweight", "bold")
        xlabel_color = xlabel_config.get("color", "black")
        
        # 设置x轴标签
        ax.set_xlabel(xlabel_text, fontsize=xlabel_fontsize, fontweight=xlabel_fontweight, color=xlabel_color)
        
        # 设置x轴范围
        x_limit_factor = style.get("x_limit_factor", 1.15)
        ax.set_xlim(0, max(sizes) * x_limit_factor)
        
        # 设置x轴刻度
        xtick_config = style.get("xtick_config", {})
        xtick_fontsize = xtick_config.get("fontsize", 10)
        xtick_fontweight = xtick_config.get("fontweight", "normal")
        xtick_color = xtick_config.get("color", "black")
        xtick_rotation = xtick_config.get("rotation", 0)
        
        # 应用x轴刻度样式
        ax.tick_params(axis='x', which='major', 
                      labelsize=xtick_fontsize, 
                      length=3, 
                      width=0.5,
                      labelcolor=xtick_color,
                      rotation=xtick_rotation)
        
        # 设置x轴刻度标签的字体权重
        for tick_label in ax.get_xticklabels():
            tick_label.set_fontweight(xtick_fontweight)
        
        print(f"🔍 Debug: X-axis label fontsize: {xlabel_fontsize}")
        print(f"🔍 Debug: X-axis tick fontsize: {xtick_fontsize}")
        
        # 添加数值标签
        if style.get("show_values", True):
            value_fontsize = style.get("value_fontsize", 12)  # 提高默认字体大小
            value_fontweight = style.get("value_fontweight", "bold")
            value_color = style.get("value_color", "black")
            
            print(f"🔍 Debug: Value labels fontsize set to: {value_fontsize}")
            
            for i, (bar, size) in enumerate(zip(bars, sizes)):
                # 在柱子右端添加百分比标签
                width = bar.get_width()
                text_obj = ax.text(
                    width + max(sizes) * 0.01,  # 稍微偏右
                    bar.get_y() + bar.get_height()/2,
                    f'{size:.1f}%',
                    ha='left',
                    va='center',
                    fontsize=value_fontsize,
                    fontweight=value_fontweight,
                    color=value_color
                )
        
        # 设置网格
        if style.get("show_grid", True):
            ax.grid(
                True, 
                axis='x', 
                alpha=style.get("grid_alpha", 0.3),
                linestyle=style.get("grid_linestyle", '--'),
                linewidth=style.get("grid_linewidth", 0.5)
            )
            ax.set_axisbelow(True)
        
        # 设置标题
        title = style.get("title", "GPU Kernel Performance Distribution")
        title_fontsize = style.get("title_fontsize", 14)
        if title:
            ax.set_title(title, fontsize=title_fontsize, fontweight='bold', pad=20, color='black')
        
        # 设置坐标轴样式
        ax.spines['top'].set_visible(False)
        ax.spines['right'].set_visible(False)
        ax.spines['left'].set_linewidth(0.5)
        ax.spines['bottom'].set_linewidth(0.5)
        
        # 设置刻度样式 - 注意：x轴样式已在上面单独设置
        ax.tick_params(axis='y', which='major', length=3, width=0.5, pad=15)  # y轴不设置labelsize
        
        # 根据字体大小动态调整左边距
        left_margin = max(0.15, min(0.4, label_fontsize * 0.02))
        plt.subplots_adjust(left=left_margin)
        
        # 调试输出：确认字体大小设置
        print(f"🔍 Debug: Y-axis label fontsize set to: {label_fontsize}")
        print(f"🔍 Debug: Y-axis labels: {labels[:3]}...")  # 显示前3个标签
        
        # 反转y轴以使最大值在顶部
        ax.invert_yaxis()
        
        # 添加统计信息框（如果需要）
        if self.custom_config.get("add_statistics_box", False):
            self._add_statistics_box(ax, labels, sizes)
        
        plt.tight_layout()
        
        if save_path:
            plt.savefig(save_path, dpi=300, bbox_inches='tight', facecolor='white', 
                       edgecolor='none', format='png')
            print(f"📊 Chart saved to: {save_path}")
        
        if show_plot:
            plt.show()
        else:
            plt.close()
    
    def _add_statistics_box(self, ax, labels, sizes):
        """Add academic-style statistics box."""
        summary = self.data.get('profiling_summary', {})
        if not summary:
            return
            
        fps = 1000 / summary.get('avg_frame_time_ms', 16.67)
        
        stats_text = f"""Performance Summary:
Components: {len(labels)}
Top 3 Total: {sum(sorted(sizes, reverse=True)[:3]):.1f}%
Estimated FPS: {fps:.1f}
Frame Time: {summary.get('avg_frame_time_ms', 0):.2f} ms"""
        
        ax.text(
            0.98, 0.98,
            stats_text,
            transform=ax.transAxes,
            fontsize=9,
            va='top',
            ha='right',
            bbox=dict(boxstyle="round,pad=0.4", facecolor='lightgray', 
                     alpha=0.8, edgecolor='black', linewidth=0.5)
        )
    
    def print_available_kernels(self):
        """Print all available kernels with detailed info."""
        print("\n" + "="*80)
        print("📋 AVAILABLE KERNELS FOR CONFIGURATION")
        print("="*80)
        
        print(f"\n{'Original Name':<35} {'Display Name':<30} {'Percentage':<12} {'Time(ms)':<10}")
        print("-" * 87)
        
        for _, row in self.df.iterrows():
            time_ms = row.get('avg_time_ms', 0)
            print(f"{row['name']:<35} {row['display_name']:<30} {row['percentage']:<11.2f}% {time_ms:<9.3f}")
        
        print(f"\nTotal kernels available: {len(self.df)}")
        print(f"Total percentage: {self.df['percentage'].sum():.1f}%")
        
        # 显示配置示例 - 包含新的标签样式配置
        print(f"\n💡 Configuration Example (with enhanced labels):")
        print('    "chart_style": {')
        print('        "figsize": [12, 8],')
        print('        "ylabel_config": {')
        print('            "fontsize": 11,')
        print('            "fontweight": "bold",')
        print('            "color": "black",')
        print('            "use_bbox": true,')
        print('            "bbox_alpha": 0.8,')
        print('            "bbox_color": "white",')
        print('            "bbox_edge_color": "lightgray"')
        print('        },')
        print('        "xlabel_config": {')
        print('            "text": "Performance (%)",     // X轴标签文字')
        print('            "fontsize": 14,                 // X轴标签字体大小')
        print('            "fontweight": "bold",           // X轴标签字体粗细')
        print('            "color": "black"                // X轴标签颜色')
        print('        },')
        print('        "xtick_config": {')
        print('            "fontsize": 12,                 // X轴刻度数字大小')
        print('            "fontweight": "normal",         // X轴刻度字体粗细')
        print('            "color": "black",               // X轴刻度颜色')
        print('            "rotation": 0                   // X轴刻度旋转角度')
        print('        },')
        print('        "value_fontsize": 14,              // 百分比数字大小')
        print('        "value_fontweight": "bold",        // 百分比数字粗细')
        print('        "value_color": "black",            // 百分比数字颜色')
        print('        "x_limit_factor": 1.2              // X轴范围扩展因子')
        print('    },')
        print('    "manual_display": [')
        for i, (_, row) in enumerate(self.df.head(3).iterrows()):
            print(f'        {{"type": "individual", "kernel": "{row["name"]}", "display_name": "{row["display_name"]}"}}{"," if i < 2 else ""}')
        print('    ]')
    
    def print_summary(self):
        """Print detailed summary."""
        summary = self.data.get('profiling_summary', {})
        
        print("\n" + "="*60)
        print("🎓 ACADEMIC GPU PERFORMANCE ANALYZER (BAR CHART)")
        print("="*60)
        
        if summary:
            print(f"\n📊 SIMULATION DATA:")
            print(f"{'Total Frames:':<20} {summary.get('total_frames', 0):,}")
            print(f"{'Average Frame Time:':<20} {summary.get('avg_frame_time_ms', 0):.3f} ms")
            print(f"{'Estimated FPS:':<20} {1000/summary.get('avg_frame_time_ms', 16.67):.1f}")
        
        print(f"\n📈 KERNEL STATISTICS:")
        print(f"{'Total Kernels:':<20} {len(self.df)}")
        print(f"{'Top Kernel:':<20} {self.df.iloc[0]['display_name']} ({self.df.iloc[0]['percentage']:.1f}%)")


def main():
    """Main function with academic focus."""
    parser = argparse.ArgumentParser(description='Academic style GPU profiler analyzer - Bar Chart Version')
    
    parser.add_argument('json_file', help='Path to GPU profiling results JSON file')
    parser.add_argument('--config', '-c', help='Custom configuration JSON file')
    parser.add_argument('--output-dir', '-o', help='Output directory')
    parser.add_argument('--show-kernels', action='store_true', help='Show available kernels')
    parser.add_argument('--no-display', '-n', action='store_true', help='Save without displaying')
    parser.add_argument('--verbose', '-v', action='store_true', help='Verbose output')
    parser.add_argument('--debug', action='store_true', help='Debug mode with detailed output')
    
    args = parser.parse_args()
    
    try:
        print("🎓 Academic GPU Performance Analyzer (Bar Chart Version)")
        print("=" * 55)
        
        # Load configuration
        custom_config = {}
        if args.config:
            try:
                with open(args.config, 'r', encoding='utf-8') as f:
                    custom_config = json.load(f)
                print(f"✅ Loaded configuration from: {args.config}")
                if args.debug:
                    print(f"📋 Configuration content:")
                    print(json.dumps(custom_config, indent=2))
            except Exception as e:
                print(f"❌ Error loading config: {e}")
                return 1
        
        # Initialize analyzer
        analyzer = AcademicGPUBarAnalyzer(args.json_file, custom_config)
        
        # Set output directory
        if args.output_dir:
            analyzer.output_dir = Path(args.output_dir)
            analyzer.output_dir.mkdir(parents=True, exist_ok=True)
        
        # Show kernels if requested
        if args.show_kernels:
            analyzer.print_available_kernels()
            return 0
        
        # Print summary
        analyzer.print_summary()
        
        # Generate chart
        show_plot = not args.no_display
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        chart_path = analyzer.output_dir / f'academic_gpu_bar_chart_{timestamp}.png'
        
        analyzer.create_academic_bar_chart(save_path=chart_path, show_plot=show_plot)
        
        print(f"\n✅ Analysis complete!")
        print(f"📁 Chart saved to: {chart_path}")
        
    except Exception as e:
        print(f"❌ Error: {e}")
        if args.verbose or args.debug:
            import traceback
            traceback.print_exc()
        return 1
    
    return 0


if __name__ == '__main__':
    exit(main())