﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NegativeEncoder.Presets
{
    public static class PresetProvider
    {
        public static void InitPresetAutoSave(MenuItem presetMenuItems)
        {
            //AppContext.PresetContext.CurrentPreset.PropertyChanged += CurrentPreset_PropertyChanged;

            ReBuildPresetMenu(presetMenuItems);
        }

        public static async void CurrentPreset_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //检查预设是否合法
            var preset = AppContext.PresetContext.CurrentPreset;

            //非QSV
            if (preset.Encoder != Encoder.QSV)
            {
                //编码器非QSV的时候禁止选择LA/LA-ICQ和QVBR模式
                if (preset.EncodeMode == EncodeMode.LA || preset.EncodeMode == EncodeMode.LAICQ || preset.EncodeMode == EncodeMode.QVBR)
                {
                    AppContext.PresetContext.CurrentPreset.EncodeMode = EncodeMode.VBR;
                }

                //编码器非QSV的时候禁止选择D3D模式
                if (preset.D3DMode != D3DMode.Auto)
                {
                    AppContext.PresetContext.CurrentPreset.D3DMode = D3DMode.Auto;
                }
            }

            //非NVENC
            if (preset.Encoder != Encoder.NVENC)
            {
                //编码器非NVENC时，只能使用8 bit模式
                if (preset.ColorDepth != ColorDepth.C8Bit)
                {
                    AppContext.PresetContext.CurrentPreset.ColorDepth = ColorDepth.C8Bit;
                }
            }

            //目标HDR格式不为SDR时，SDR转换只能是None
            if (preset.NewHdrType != HdrType.SDR)
            {
                if (preset.Hdr2SdrMethod != Hdr2Sdr.None)
                {
                    AppContext.PresetContext.CurrentPreset.Hdr2SdrMethod = Hdr2Sdr.None;
                }
            }

            //标记当前预设已修改
            AppContext.PresetContext.IsPresetEdit = true;

            //存储预设到文件
            await SystemOptions.SystemOption.SaveOption(AppContext.PresetContext.CurrentPreset);
        }

        public static void ReBuildPresetMenu(MenuItem presetMenuItems)
        {
            var deletable = new List<MenuItem>();

            foreach (var presetSubmenu in presetMenuItems.Items)
            {
                if (presetSubmenu is MenuItem submenu)
                {
                    if (submenu.IsCheckable == true)
                    {
                        deletable.Add(submenu);
                    }
                }
            }

            foreach (var deleteSubmenu in deletable)
            {
                presetMenuItems.Items.Remove(deleteSubmenu);
            }

            AppContext.PresetContext.IsPresetEdit = true;

            if (AppContext.PresetContext.PresetList.Count == 0)
            {
                var emptySubMenu = new MenuItem
                {
                    Header = "(空)",
                    IsCheckable = true,
                    IsEnabled = false,
                    IsChecked = false
                };
                presetMenuItems.Items.Add(emptySubMenu);

                return;
            }

            foreach (var preset in AppContext.PresetContext.PresetList)
            {
                var presetSubMenu = new MenuItem
                {
                    Header = preset.PresetName,
                    IsCheckable = true
                };
                presetSubMenu.Click += PresetSubMenu_Click;
                if (preset.PresetName == AppContext.PresetContext.CurrentPreset.PresetName)
                {
                    presetSubMenu.IsChecked = true;

                    if (Utils.DeepCompare.EqualsDeep1(preset, AppContext.PresetContext.CurrentPreset))
                    {
                        AppContext.PresetContext.IsPresetEdit = false;
                    }
                }

                presetMenuItems.Items.Add(presetSubMenu);
            }
        }

        private static async void PresetSubMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem m)
            {
                if (AppContext.PresetContext.IsPresetEdit)
                {
                    if (MessageBox.Show("当前预设未保存，是否放弃？", "预设", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    {
                        ReBuildPresetMenu(m.Parent as MenuItem);
                        return;
                    }
                }

                var checkName = m.Header as string;

                foreach(var p in AppContext.PresetContext.PresetList)
                {
                    if (p.PresetName == checkName)
                    {
                        AppContext.PresetContext.CurrentPreset = Utils.DeepCompare.CloneDeep1(p);
                        break;
                    }
                }

                ReBuildPresetMenu(m.Parent as MenuItem);
                //存储预设到文件
                await SystemOptions.SystemOption.SaveOption(AppContext.PresetContext.CurrentPreset);
            }
        }

        public static async Task LoadPresetAutoSave()
        {
            AppContext.PresetContext.CurrentPreset = await SystemOptions.SystemOption.ReadOption<Preset>();
            AppContext.PresetContext.PresetList = (await SystemOptions.SystemOption.ReadListOption<Preset>()).OrderBy(it => it.PresetName).ToList();
        }

        public static void NewPreset(Window parentWindow = null)
        {
            if (AppContext.PresetContext.IsPresetEdit)
            {
                var res = MessageBox.Show(parentWindow, "当前预设未保存，是否确认覆盖", "预设", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            AppContext.PresetContext.CurrentPreset = new Preset();
            AppContext.PresetContext.IsPresetEdit = true;
        }

        public static async Task SavePreset(MenuItem presetMenuItems)
        {
            var presetName = AppContext.PresetContext.CurrentPreset.PresetName;
            if (AppContext.PresetContext.PresetList.Count(p => p.PresetName == presetName) > 0)
            {
                for (var p = 0; p < AppContext.PresetContext.PresetList.Count; p++)
                {
                    if (AppContext.PresetContext.PresetList[p].PresetName == presetName)
                    {
                        AppContext.PresetContext.PresetList[p] = AppContext.PresetContext.CurrentPreset;
                        await SystemOptions.SystemOption.SaveListOption(AppContext.PresetContext.PresetList);
                        break;
                    }
                }
            }
            else
            {
                await AddPresetList(presetMenuItems, AppContext.PresetContext.CurrentPreset);
            }

            ReBuildPresetMenu(presetMenuItems);
            AppContext.PresetContext.IsPresetEdit = false;
        }

        public static async Task SaveAsPreset(MenuItem presetMenuItems, string newName)
        {
            AppContext.PresetContext.CurrentPreset = Utils.DeepCompare.CloneDeep1(AppContext.PresetContext.CurrentPreset);
            AppContext.PresetContext.CurrentPreset.PresetName = newName;

            await SavePreset(presetMenuItems);
        }

        private static async Task AddPresetList(MenuItem presetMenuItems, Preset currentPreset)
        {
            AppContext.PresetContext.PresetList.Add(currentPreset);
            presetMenuItems.Items.Add(new MenuItem
            {
                Header = currentPreset.PresetName,
                IsCheckable = true,
                IsChecked = true
            });

            await SystemOptions.SystemOption.SaveListOption(AppContext.PresetContext.PresetList);
        }
    }
}
