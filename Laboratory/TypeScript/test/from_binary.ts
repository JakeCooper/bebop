import * as fs from 'fs';
import { Library } from './generated/gen';

it('can parse Library from binary file', () => {
    var buffer = fs.readFileSync('test/jazz-library.bin');
    var library = Library.decode(buffer);
    expect(library.songs.size).toEqual(1);
    var donnaLee = '81c6987b-48b7-495f-ad01-ec20cc5f5be1';
    expect(library.songs.get(donnaLee).title).toEqual('Donna Lee');
    expect(library.songs.get(donnaLee).performers[1].name).toEqual('Miles Davis');
});
