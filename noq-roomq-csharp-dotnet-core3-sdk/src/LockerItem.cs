namespace NoQ.RoomQ
{
    public class LockerItem
    {
        public string key { get; set; }
        public string value { get; set; }
        public int limit { get; set; }
        public int kvLimit { get; set; }

        /**
          * LockerItem constructor.
          * @param string key
          * @param string value
          * @param int limit max number of values can be stored in this key
          * @param int kvLimit max number of this key-value pair can be stored in all the lockers in this room
          */
        public LockerItem(string key, string value, int limit, int kvLimit)
        {
            this.key = key;
            this.value = value;
            this.limit = limit;
            this.kvLimit = kvLimit;
        }
    }
}
