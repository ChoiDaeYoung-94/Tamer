// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("/TEdZDc654h0ldyqwyl8ZMPz+Hhxd5HvCdWuah7EVGupgbeXfOVA+v1+cH9P/X51ff1+fn+0jhBclChu923gS4lwkUk/0i/r+Ha9VJ0GaejHohwtUZYnKEG1iu+asm308ksQYrB40p4+CJmwlgVW42tmDbGWtu2Q6zJSO8APTinxyYS8OcCjKszwAqziaWi/lrI693+yg+mVaZAWFTTj5ZoXgjiZdSRRjULWLtYzA9f4wQGHSCQjX3SZRgtlx4sJsx7Um1t6khP9SLIETenlPXNwVPUVfIe1DM24+Qf+O8aZDtEHLx6jhOI1khJCABzUNpa7uj1faA0b2mMCWzfV4U3B419P/X5dT3J5dlX5N/mIcn5+fnp/fH9K81++xLDBrH18fn9+");
        private static int[] order = new int[] { 11,13,13,8,9,6,7,8,8,10,13,12,13,13,14 };
        private static int key = 127;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
