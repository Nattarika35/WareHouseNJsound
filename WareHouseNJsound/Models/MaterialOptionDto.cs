namespace WareHouseNJsound.Models
{
    public class MaterialOptionDto
    {
        public string Materials_ID { get; set; }
        public string MaterialsName { get; set; }
        public int? Unit_ID { get; set; }
        public string UnitName { get; set; }
        public int StockLeft { get; set; }
    }
}
