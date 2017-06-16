using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LogParser
{
    public sealed class ReverseLineReader : IEnumerable<string>
    {

        private readonly Func<Stream> _streamSource;

        private readonly Encoding _encoding;

        private readonly int _bufferSize;

        private long _position;

        private readonly Func<long, byte, bool> characterStartDetector;

        public long OffSet {
            get { return _position; }
        }

        public ReverseLineReader(string filename, long position)
        {
            _streamSource = () => File.OpenRead(filename);
            _encoding = Encoding.UTF8;
            _bufferSize = 4096;
            _position = position;

            if (_encoding.IsSingleByte)
            {
                // For a single byte encoding, every byte is the start (and end) of a character
                characterStartDetector = (pos, data) => true;
            }
            else if (_encoding is UnicodeEncoding)
            {
                // For UTF-16, even-numbered positions are the start of a character
                characterStartDetector = (pos, data) => (pos & 1) == 0;
            }
            else if (_encoding is UTF8Encoding)
            {
                // For UTF-8, bytes with the top bit clear or the second bit set are the start of a character
                // See http://www.cl.cam.ac.uk/~mgk25/unicode.html
                characterStartDetector = (pos, data) => (data & 0x80) == 0 || (data & 0x40) != 0;
            }
            else
            {
                throw new ArgumentException("Only single byte, UTF-8 and Unicode encodings are permitted");
            }
        }

        public IEnumerator<string> GetEnumerator()
        {
            Stream stream = _streamSource();
            if (!stream.CanSeek)
            {
                stream.Dispose();
                throw new NotSupportedException("Unable to seek within stream");
            }
            if (!stream.CanRead)
            {
                stream.Dispose();
                throw new NotSupportedException("Unable to read within stream");
            }
            return GetEnumeratorImpl(stream);
        }

        private IEnumerator<string> GetEnumeratorImpl(Stream stream)
        {
            try
            {
                if (_encoding is UnicodeEncoding && (_position & 1) != 0)
                {
                    throw new InvalidDataException("UTF-16 encoding provided, but stream has odd length.");
                }

                byte[] buffer = new byte[_bufferSize + 2];
                char[] charBuffer = new char[_encoding.GetMaxCharCount(buffer.Length)];
                int leftOverData = 0;
                String previousEnd = null;

                bool firstYield = true;

                bool swallowCarriageReturn = false;

                while (_position > 0)
                {
                    int bytesToRead = Math.Min(_position > int.MaxValue ? _bufferSize : (int)_position, _bufferSize);

                    _position -= bytesToRead;
                    stream.Position = _position;
                    StreamUtil.ReadExactly(stream, buffer, bytesToRead);
                    // If we haven't read a full buffer, but we had bytes left
                    // over from before, copy them to the end of the buffer
                    if (leftOverData > 0 && bytesToRead != _bufferSize)
                    {

                        Array.Copy(buffer, _bufferSize, buffer, bytesToRead, leftOverData);
                    }
                    // We've now *effectively* read this much data.
                    bytesToRead += leftOverData;

                    int firstCharPosition = 0;
                    while (!characterStartDetector(_position + firstCharPosition, buffer[firstCharPosition]))
                    {
                        firstCharPosition++;

                        if (firstCharPosition == 3 || firstCharPosition == bytesToRead)
                        {
                            throw new InvalidDataException("Invalid UTF-8 data");
                        }
                    }
                    leftOverData = firstCharPosition;

                    int charsRead = _encoding.GetChars(buffer, firstCharPosition, bytesToRead - firstCharPosition, charBuffer, 0);
                    int endExclusive = charsRead;

                    for (int i = charsRead - 1; i >= 0; i--)
                    {
                        char lookingAt = charBuffer[i];
                        if (swallowCarriageReturn)
                        {
                            swallowCarriageReturn = false;
                            if (lookingAt == '\r')
                            {
                                endExclusive--;
                                continue;
                            }
                        }
                        // Anything non-line-breaking, just keep looking backwards
                        if (lookingAt != '\n' && lookingAt != '\r')
                        {
                            continue;
                        }
                        // End of CRLF? Swallow the preceding CR
                        if (lookingAt == '\n')
                        {
                            swallowCarriageReturn = true;
                        }
                        int start = i + 1;
                        string bufferContents = new string(charBuffer, start, endExclusive - start);
                        endExclusive = i;
                        string stringToYield = previousEnd == null ? bufferContents : bufferContents + previousEnd;
                        if (!firstYield || stringToYield.Length != 0)
                        {
                            yield return stringToYield;
                        }
                        firstYield = false;
                        previousEnd = null;
                    }

                    previousEnd = endExclusive == 0 ? null : (new string(charBuffer, 0, endExclusive) + previousEnd);

                    // If we didn't decode the start of the array, put it at the end for next time
                    if (leftOverData != 0)
                    {
                        Buffer.BlockCopy(buffer, 0, buffer, _bufferSize, leftOverData);
                    }
                }
                if (leftOverData != 0)
                {
                    // At the start of the final buffer, we had the end of another character.
                    throw new InvalidDataException("Invalid UTF-8 data at start of stream");
                }
                if (firstYield && string.IsNullOrEmpty(previousEnd))
                {
                    yield break;
                }
                yield return previousEnd ?? "";
            }
            finally
            {
                stream.Dispose();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public static class StreamUtil
    {
        public static void ReadExactly(Stream input, byte[] buffer, int bytesToRead)
        {
            int index = 0;
            while (index < bytesToRead)
            {
                int read = input.Read(buffer, index, bytesToRead - index);
                if (read == 0)
                {
                    throw new EndOfStreamException
                        (String.Format("End of stream reached with {0} byte{1} left to read.",
                                       bytesToRead - index,
                                       bytesToRead - index == 1 ? "s" : ""));
                }
                index += read;
            }
        }
    }
}
