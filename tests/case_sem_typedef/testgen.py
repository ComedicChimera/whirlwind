import os, argparse, string
from random import choice, randint

def gen_name():
    return choice(string.ascii_letters) + ''.join(choice(string.ascii_letters + string.digits) for _ in range(randint(5, 15)))

def gen_type(defs):
    type_to_gen = randint(0, 19)
    if type_to_gen % 2 == 0:
        if len(defs) == 0:
            return 'any'

        return choice(list(defs.keys()))
    else:
        return [
            'int',
            'uint',
            'double',
            'string',
            'rune',
            'float',
            'ushort',
            'byte',
            'long',
            'bool'
        ][type_to_gen // 2]

def gen_def(file, name, defs):
    def_kind = randint(0, 2)

    if def_kind == 2:
        file.write('closed ')

    file.write(f'type {name}')

    # type set
    if def_kind == 0:
        file.write(f' = {" | ".join(gen_type(defs) for _ in range(randint(3, 5)))}')
    # struct
    elif def_kind == 1:
        file.write('{\n')

        for i in range(randint(2, 5)):
            file.write(f'\tfield{i}: {gen_type(defs)}\n')

        file.write('}')
    # alg type
    elif def_kind == 2:
        for i in range(randint(2, 5)):
            arity = randint(0, 2)

            if arity == 0:
                file.write(f'\n\t| Variant{i}')
            else:
                file.write(f'\n\t| Variant{i}({", ".join(gen_type(defs) for _ in range(arity))})')

    file.write('\n\n')

def gen_file(file, defs, package_count):
    local_defs = {}
    for _ in range(randint(10, 30)):
        name = gen_name()

        if name in defs:
            continue

        local_defs[name] = None
        defs[name] = None

    for name in local_defs:
        gen_def(file, name, defs)
        

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('-p', '--packages', type=int, default=1)
    parser.add_argument('-f', '--files', type=int, default=10)

    args = parser.parse_args()

    os.mkdir('test_typedef')
    os.chdir('test_typedef')

    package_count = args.packages
    for i in range(package_count):
        package_name = f'pkg{i}'
        os.mkdir(package_name)
        os.chdir(package_name)

        os.system(f'whirl mod init {package_name}')

        definitions = {}
        for j in range(args.files):
            with open(f'file{j}.wrl', 'w') as f:
                gen_file(f, definitions, package_count)

        os.chdir('../')