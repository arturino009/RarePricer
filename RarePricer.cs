using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using Newtonsoft.Json;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace RarePricer
{
    public class RarePricer : BaseSettingsPlugin<Settings>
    {
        public static List<RenderableItem> allItems = new List<RenderableItem>();

        public static Queue<RenderableItem> priceList = new Queue<RenderableItem>();

        public List<RenderableItem> RenderedItems = new List<RenderableItem>();

        public List<List<string>> HoveredItems = new List<List<string>>();

        private int running;
        public override void Render()
        {
            base.Render();

            var inventoryPanel = GameController.Game.IngameState.IngameUi.InventoryPanel;
            var inventoryItems = inventoryPanel[InventoryIndex.PlayerInventory].VisibleInventoryItems;

            var uiHover = GameController.IngameState.UIHover;
            if (inventoryPanel.IsVisibleLocal) {
                if (uiHover.IsVisibleLocal)
                {
                    var itemType = uiHover.AsObject<HoverItemIcon>()?.ToolTipType ?? null;
                    if (itemType != null && itemType != ToolTipType.ItemInChat && itemType != ToolTipType.None)
                    {
                        var hoverItem = uiHover.AsObject<NormalInventoryItem>();
                        if (hoverItem.Item?.Path != null && (hoverItem.Tooltip?.IsValid ?? false))
                        {
                            var item = hoverItem.Item;
                            var mods = item.GetComponent<Mods>();
                            if (mods != null && mods.Identified && mods.ItemRarity.ToString() == "Rare")
                            {
                                if (!HoveredItems.Any(c => c.SequenceEqual(mods.HumanStats)))
                                {
                                    SendKeys.SendWait("^{c}");
                                    var itemDescription = GetClipboard();
                                    RenderableItem a = new RenderableItem();
                                    a.fullitem = hoverItem;
                                    a.itemDescription = itemDescription;
                                    a.itemMods = mods.HumanStats;

                                    if (!allItems.Exists(x => x.itemDescription == itemDescription))
                                    {
                                        allItems.Add(a);
                                        priceList.Enqueue(a);
                                    }
                                    HoveredItems.Add(mods.HumanStats);
                                }

                                foreach (var i in RenderedItems)        //update position on moved item, but don't recalculate price
                                {
                                    if (Enumerable.SequenceEqual(i.itemMods, mods.HumanStats) && hoverItem != i.fullitem)
                                    {
                                        i.fullitem = hoverItem;
                                        break;
                                    }
                                }  
                            }
                        }
                    }
                }
                foreach (var item in RenderedItems)
                {
                    if(item.fullitem.IsVisibleLocal && inventoryItems.Contains(item.fullitem))
                        RenderItem(item);
                }
            }
            if (priceList.Count != 0)
            {
                if (Interlocked.CompareExchange(ref running, 1, 0) == 0)
                {
                    Thread t = new Thread
                    (
                        () =>
                        {
                            try
                            {
                                DequeItems();
                            }
                            catch
                            {
                            }
                            finally
                            {
                                //Regardless of exceptions, we need this to happen:
                                running = 0;
                            }
                        }
                    );
                    t.IsBackground = true;
                    t.Name = "myThread";
                    t.Start();
                }
            }
        }

        public Tuple<double, double, string> GetPrice(RenderableItem item)
        {
            var encodedString = Base64Encode(item.itemDescription);
            var url = @"https://www.poeprices.info/api?l=Expedition&i=";
            WebRequest wrGETURL = WebRequest.Create(url + encodedString);

            Stream objStream;
            objStream = wrGETURL.GetResponse().GetResponseStream();

            StreamReader objReader = new StreamReader(objStream);

            string sLine = "";
            double min = 0;
            double max = 0;
            var currency = "";

            sLine = objReader.ReadLine();
            if (sLine != null)
            {
                var result = JsonConvert.DeserializeObject<dynamic>(sLine);
                if (result.error == 0)
                {
                    min = result.min;
                    max = result.max;
                    currency = result.currency;
                    LogMessage("Got price: " + min + currency);
                }
                else
                {
                    LogMessage("Error: " + result.error_msg);
                }
            }
            return Tuple.Create(min, max, currency);

        }

        public void DequeItems()
        {
            try
            {
                var itemA = priceList.Dequeue();
                var value = GetPrice(itemA);
                RenderableItem a = new RenderableItem();
                a.fullitem = itemA.fullitem;
                a.itemMods = itemA.itemMods;
                a.itemDescription = itemA.itemDescription;
                a.min = value.Item1;
                a.max = value.Item2;
                a.currency = value.Item3;
                RenderedItems.Add(a);
                Thread.Sleep(1000);
            }
            catch { }
        }
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        public string GetClipboard()
        {
            IDataObject idat = null;
            Exception threadEx = null;
            String text = "";
            Thread staThread = new Thread(
                delegate ()
                {
                    try
                    {
                        idat = Clipboard.GetDataObject();
                        text = (string)idat.GetData(DataFormats.Text);
                    }
                    catch (Exception ex)
                    {
                        threadEx = ex;
                    }
                });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();
            return text;
        }

        private void RenderItem(RenderableItem inventoryItem)
        {
            var item = inventoryItem.fullitem;
            var price = inventoryItem.min;
            var currency = inventoryItem.currency;
            var rect = item.GetClientRect();

            Graphics.DrawText(price + currency, rect.TopLeft, Color.White, 30);
        }
    }

    public class RenderableItem
    {
        public NormalInventoryItem fullitem;
        public List<string> itemMods;
        public string itemDescription;
        public double min;
        public double max;
        public string currency;
    }
}