#!/usr/bin/env python3
"""
Academic Style GPU Profiler Analyzer
学术风格GPU性能分析器，专注于清晰、专业的图表展示
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

class AcademicGPUAnalyzer:
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
    
    def create_academic_pie_chart(self, save_path=None, show_plot=True):
        """Create clean, academic-style pie chart."""
        
        # Prepare data
        labels, sizes, custom_colors = self.prepare_display_data()
        
        if not labels:
            print("❌ No data to display. Check your configuration.")
            return
        
        # Get styling configuration
        style = self.custom_config.get("chart_style", {})
        figsize = style.get("figsize", [10, 8])
        
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
        
        # Create pie chart with academic styling
        wedge_props = {
            'width': style.get("donut_width", 0.4), 
            'edgecolor': style.get("edge_color", "white"),
            'linewidth': style.get("edge_width", 1.5)
        }
        
        # Determine if using external labels
        use_external = style.get("use_external_labels", True)
        
        pie_args = {
            'labels': None if use_external else labels,
            'autopct': '%1.1f%%' if style.get("show_percentages", False) else None,
            'startangle': style.get("start_angle", 90),
            'colors': final_colors,
            'wedgeprops': wedge_props,
            'pctdistance': style.get("pctdistance", 0.75)
        }
        
        # Handle different return values based on autopct setting
        if pie_args['autopct'] is not None:
            wedges, texts, autotexts = ax.pie(sizes, **pie_args)
            # Style the percentage text for academic clarity
            if autotexts:
                for autotext in autotexts:
                    autotext.set_color('black')
                    autotext.set_fontweight('bold')
                    autotext.set_fontsize(style.get("percentage_fontsize", 10))
                    autotext.set_bbox(dict(facecolor='white', alpha=0.8, edgecolor='none', pad=1))
        else:
            # When autopct is None, pie() returns only wedges and texts
            result = ax.pie(sizes, **pie_args)
            wedges = result[0]
            texts = result[1] if len(result) > 1 else []
            autotexts = None
        
        # Add external labels if requested
        if use_external:
            self._add_academic_labels(ax, wedges, labels, sizes, style)
        
        # Set title with academic formatting
        title = style.get("title", "GPU Kernel Time Distribution")
        title_fontsize = style.get("title_fontsize", 14)
        #ax.set_title(title, fontsize=title_fontsize, fontweight='bold', pad=20, color='black')
        
        # Add legend if requested
        if self.custom_config.get("show_legend", False):
            self._add_academic_legend(ax, labels, final_colors, sizes)
        
        # Add statistics box if requested
        if self.custom_config.get("add_statistics_box", False):
            self._add_statistics_box(ax, labels, sizes)
        
        # Set equal aspect ratio for perfect circle
        ax.set_aspect('equal')
        
        plt.tight_layout()
        
        if save_path:
            plt.savefig(save_path, dpi=300, bbox_inches='tight', facecolor='white', 
                       edgecolor='none', format='png')
            print(f"📊 Chart saved to: {save_path}")
        
        if show_plot:
            plt.show()
        else:
            plt.close()
    
    def _add_academic_labels(self, ax, wedges, labels, sizes, style):
        """Add clean academic-style external labels."""
        label_distance = style.get("label_distance", 1.25)
        label_fontsize = style.get("label_fontsize", 10)
        
        # Academic style bbox
        bbox_props = dict(
            boxstyle="round,pad=0.3", 
            facecolor="white", 
            edgecolor='lightgray', 
            alpha=0.95,
            linewidth=0.5
        )
        
        for i, (wedge, label, size) in enumerate(zip(wedges, labels, sizes)):
            # Calculate angle
            angle = (wedge.theta2 - wedge.theta1) / 2.0 + wedge.theta1
            angle_rad = np.deg2rad(angle)
            
            # Calculate positions
            x = label_distance * np.cos(angle_rad)
            y = label_distance * np.sin(angle_rad)
            
            # Connection point
            conn_x = 1.02 * np.cos(angle_rad)
            conn_y = 1.02 * np.sin(angle_rad)
            
            # Text alignment
            ha = 'center'
            
            # Draw clean connection line
            ax.plot([conn_x, x * 0.95], [conn_y, y * 0.95], 
                   color='gray', alpha=0.6, linewidth=1, linestyle='-')
            
            # Add label with academic formatting
            label_text = f"{label}\n({size:.1f}%)"
            ax.annotate(
                label_text,
                xy=(x, y),
                ha=ha,
                va='center',
                fontsize=label_fontsize,
                fontweight='normal',
                bbox=bbox_props,
                color='black'
            )
    
    def _add_academic_legend(self, ax, labels, colors, sizes):
        """Add clean academic legend."""
        import matplotlib.patches as mpatches
        
        legend_elements = []
        for label, color, size in zip(labels, colors, sizes):
            legend_elements.append(
                mpatches.Patch(color=color, label=f'{label} ({size:.1f}%)')
            )
        
        legend = ax.legend(
            handles=legend_elements, 
            loc='center left', 
            bbox_to_anchor=(1.1, 0.5), 
            frameon=True,
            fancybox=False, 
            shadow=False, 
            ncol=1,
            fontsize=10,
            edgecolor='black',
            facecolor='white'
        )
        
        legend.get_frame().set_linewidth(0.5)
    
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
            0.02, 0.98,
            stats_text,
            transform=ax.transAxes,
            fontsize=9,
            va='top',
            ha='left',
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
        
        # 显示配置示例
        print(f"\n💡 Configuration Example:")
        print('    "manual_display": [')
        for i, (_, row) in enumerate(self.df.head(3).iterrows()):
            print(f'        {{"type": "individual", "kernel": "{row["name"]}", "display_name": "{row["display_name"]}"}}{"," if i < 2 else ""}')
        print('    ]')
    
    def print_summary(self):
        """Print detailed summary."""
        summary = self.data.get('profiling_summary', {})
        
        print("\n" + "="*60)
        print("🎓 ACADEMIC GPU PERFORMANCE ANALYZER")
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
    parser = argparse.ArgumentParser(description='Academic style GPU profiler analyzer')
    
    parser.add_argument('json_file', help='Path to GPU profiling results JSON file')
    parser.add_argument('--config', '-c', help='Custom configuration JSON file')
    parser.add_argument('--output-dir', '-o', help='Output directory')
    parser.add_argument('--show-kernels', action='store_true', help='Show available kernels')
    parser.add_argument('--no-display', '-n', action='store_true', help='Save without displaying')
    parser.add_argument('--verbose', '-v', action='store_true', help='Verbose output')
    parser.add_argument('--debug', action='store_true', help='Debug mode with detailed output')
    
    args = parser.parse_args()
    
    try:
        print("🎓 Academic GPU Performance Analyzer")
        print("=" * 45)
        
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
        analyzer = AcademicGPUAnalyzer(args.json_file, custom_config)
        
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
        chart_path = analyzer.output_dir / f'academic_gpu_chart_{timestamp}.png'
        
        analyzer.create_academic_pie_chart(save_path=chart_path, show_plot=show_plot)
        
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