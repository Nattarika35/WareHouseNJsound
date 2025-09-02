using System.Collections.Generic;

namespace WareHouseNJsound.Models
{
    public class RequestViewModel
    {
        public Request Request { get; set; }
        public List<RequestDetail> RequestDetails { get; set; }
        public List<Employee> Employees { get; internal set; }
    }
}
