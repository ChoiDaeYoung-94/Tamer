import unittest
from verify_age_choice_prefs import verify


class AgeChoicePreferencesTests(unittest.TestCase):
    def xml(self, value):
        return ('<map><string name="Tamer.Privacy.AgeChoice">' + value + '</string></map>').encode()

    def test_observed_percent_encoded_separator(self):
        self.assertEqual(verify(self.xml('1%7C13to15'), '13to15')['decoded'], '1|13to15')

    def test_decline_and_interval_remain_distinct(self):
        self.assertEqual(verify(self.xml('1%7Cdeclined'), 'declined')['decoded'], '1|declined')
        with self.assertRaises(ValueError):
            verify(self.xml('1%7C13to15'), 'declined')

    def test_corruption_version_and_double_encoding_are_rejected(self):
        for value in ('1%7declined', '2%7Cdeclined', '1%257Cdeclined', '1%7Cdeclined%20'):
            with self.subTest(value=value), self.assertRaises(ValueError):
                verify(self.xml(value), 'declined')

    def test_missing_or_duplicate_key_is_rejected(self):
        for xml in (b'<map/>', self.xml('1%7Cdeclined').replace(b'</map>',
                    b'<string name="Tamer.Privacy.AgeChoice">1%7Cdeclined</string></map>')):
            with self.assertRaises(ValueError):
                verify(xml, 'declined')


if __name__ == '__main__':
    unittest.main()
