import { BebopView } from 'bebop';
import { IOptionTest, OptionTest } from './generated/option';
import * as assert from "assert";

it("Option roundtrip", () => {
    const obj: IOptionTest = {
        optionalGuid: "01234567-0123-0123-0123-0123456789ab",
        optionalString: undefined,
        requiredGuid: "01234567-0123-0123-0123-0123456789ab",
        requiredString: "required",
        twiceOptional: 2,
        alsoTwiceOptional: undefined,
        listOfOptional: [3, undefined, 4],
        optionalList: [5, 6, 7],
    };
    const bytes = OptionTest.encode(obj);
    const obj2 = OptionTest.decode(bytes);
    expect(obj).toEqual(obj2);
});

